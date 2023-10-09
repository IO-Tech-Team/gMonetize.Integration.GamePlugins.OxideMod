#define DEBUG
// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;
using Mono.Security.X509;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("gMonetize", "gMonetize Project", "2.0.0")]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    public class gMonetize : CovalencePlugin
    {
        private const string PERMISSION_USE = "gmonetize.use";
        private const string CMD_OPEN = "gmonetize.open";
        private const string CMD_CLOSE = "gmonetize.close";
        private const string CMD_NEXT_PAGE = "gmonetize.nextpage";
        private const string CMD_PREV_PAGE = "gmonetize.prevpage";
        private const string CMD_RETRY_LOAD = "gmonetize.retryload";
        private const string CMD_REDEEM_ITEM = "gmonetize.redeemitem";

        private static gMonetize Instance;

        private PluginSettings _settings;

        #region Debug logging

        [Conditional("DEBUG")]
        private static void LogDebug(string format, params object[] args)
        {
            Interface.uMod.LogDebug("[gMonetize] " + format, args);
        }

        #endregion

        #region Configuration handling

        protected override void LoadDefaultConfig()
        {
            _settings = PluginSettings.GetDefaults();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _settings = Config.ReadObject<PluginSettings>();
            }
            catch
            {
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_settings);
        }

        #endregion

        #region Oxide hooks

        private void Init()
        {
            Instance = this;
        }

        private void OnServerInitialized()
        {
            RegisterCommands();

            if (
                string.IsNullOrWhiteSpace(_settings.ApiKey)
                || _settings.ApiKey == PluginSettings.GetDefaults().ApiKey
            )
            {
                LogWarning(
                    "API key was not set up properly. You have to specify it to allow plugin to communicate with gMonetize API"
                );
            }

            foreach (IPlayer player in players.Connected)
            {
                OnUserConnected(player);
            }
        }

        private void OnUserConnected(IPlayer player)
        {
            if (TryGetBasePlayer(player, out BasePlayer basePlayer))
            {
                basePlayer.gameObject.AddComponent<UI>();
            }
        }

        private void OnUserDisconnected(IPlayer player)
        {
            if (TryGetBasePlayer(player, out BasePlayer basePlayer))
            {
                UnityEngine.Object.Destroy(basePlayer.GetComponent<UI>());
            }
        }

        private void Unload()
        {
            foreach (IPlayer player in players.Connected)
            {
                OnUserDisconnected(player);
            }
        }

        #endregion

        private void RegisterCommands()
        {
            covalence.RegisterCommand(CMD_OPEN, this, HandleCommand);
            covalence.RegisterCommand(CMD_CLOSE, this, HandleCommand);
            covalence.RegisterCommand(CMD_NEXT_PAGE, this, HandleCommand);
            covalence.RegisterCommand(CMD_PREV_PAGE, this, HandleCommand);
            covalence.RegisterCommand(CMD_RETRY_LOAD, this, HandleCommand);
            covalence.RegisterCommand(CMD_REDEEM_ITEM, this, HandleCommand);

            foreach (var command in _settings.ChatCommands)
            {
                covalence.RegisterCommand(command, this, HandleCommand);
            }

            if (_settings.ChatCommands.Length == 0)
            {
                LogWarning("No chat commands were registered");
            }
        }

        private bool HandleCommand(IPlayer player, string command, string[] args)
        {
            BasePlayer basePlayer = player.Object as BasePlayer;

            if (!basePlayer)
            {
                LogError(
                    "BasePlayer not found while calling cmd {0} on player {1}",
                    command,
                    player
                );
                return true;
            }

            LogDebug("Command {0} by player {1}", command, player);

            switch (command)
            {
                case CMD_CLOSE:
                    basePlayer.SendMessage(nameof(UI.gMonetize_CloseUI));
                    break;

                case CMD_NEXT_PAGE:
                    basePlayer.SendMessage(nameof(UI.gMonetize_NextPage));
                    break;

                case CMD_PREV_PAGE:
                    basePlayer.SendMessage(nameof(UI.gMonetize_PreviousPage));
                    break;

                case CMD_RETRY_LOAD:
                    APIClient.GetPlayerInventory(
                        player.Id,
                        items =>
                            basePlayer.SendMessage(nameof(UI.gMonetize_InventoryReceived), items),
                        code =>
                            basePlayer.SendMessage(nameof(UI.gMonetize_InventoryReceiveFail), code)
                    );
                    basePlayer.SendMessage(nameof(UI.gMonetize_LoadingItems));
                    break;

                case CMD_REDEEM_ITEM:
                    APIClient.RedeemItem(
                        player.Id,
                        args[0],
                        () => basePlayer.SendMessage(nameof(UI.gMonetize_RedeemItemOk), args[0]),
                        code =>
                            basePlayer.SendMessage(
                                nameof(UI.gMonetize_ItemClaimedError),
                                new object[] { args[0], code }
                            )
                    );
                    basePlayer.SendMessage(nameof(UI.gMonetize_RedeemItemPending), args[0]);
                    break;

                default:
                    if (command == CMD_OPEN || _settings.ChatCommands.Contains(command))
                    {
                        basePlayer.SendMessage(nameof(UI.gMonetize_OpenUI));
                        APIClient.GetPlayerInventory(
                            player.Id,
                            items =>
                                basePlayer.SendMessage(
                                    nameof(UI.gMonetize_InventoryReceived),
                                    items
                                ),
                            code =>
                                basePlayer.SendMessage(
                                    nameof(UI.gMonetize_InventoryReceiveFail),
                                    code
                                )
                        );
                        basePlayer.SendMessage(nameof(UI.gMonetize_LoadingItems));
                    }
                    break;
            }

            return true;
        }

        private bool TryGetBasePlayer(IPlayer player, out BasePlayer basePlayer)
        {
            return (basePlayer = player.Object as BasePlayer) != null;
        }

        private bool CanRedeemItem(BasePlayer player, APIClient.InventoryEntryDto inventoryEntry)
        {
            switch (inventoryEntry.type)
            {
                case APIClient.InventoryEntryDto.InventoryEntryType.ITEM:
                    return GetPlayerAvailableSlots(player) > 0;
                case APIClient.InventoryEntryDto.InventoryEntryType.KIT:

                    break;
                case APIClient.InventoryEntryDto.InventoryEntryType.RESEARCH:
                    return !player.blueprints.IsUnlocked(
                        ItemManager.FindItemDefinition(inventoryEntry.research.researchId)
                    );
                case APIClient.InventoryEntryDto.InventoryEntryType.CUSTOM:
                    break;

                default:
                    return true;
            }

            return true;
        }

        private int GetPlayerAvailableSlots(BasePlayer player)
        {
            int containerMainSlots = player.inventory.containerMain.capacity;
            int containerBeltSlots = player.inventory.containerBelt.capacity;
            int containerMainItems = player.inventory.containerMain.itemList.Count;
            int containerBeltItems = player.inventory.containerBelt.itemList.Count;

            // TODO: Take stacking into account

            return containerMainSlots
                + containerBeltSlots
                - containerMainItems
                - containerBeltItems;
        }

        private class UI : FacepunchBehaviour
        {
            private const int ITEMS_PER_PAGE = 8 * 4;

            private BasePlayer _player;

            private bool _isOpen,
                _isItemListContainerDrawn;
            private NotificationState _notificationState;

            private List<APIClient.InventoryEntryDto> _inventory;
            private Dictionary<string, APIClient.InventoryEntryDto> _inventoryIdMap;

            private int _currentPageId;

            private void SetInventory(IEnumerable<APIClient.InventoryEntryDto> inventory = null)
            {
                if (inventory == null)
                {
                    _inventory = null;
                    _inventoryIdMap = null;
                    return;
                }

                _inventory = inventory.ToList();
                _inventoryIdMap = _inventory.ToDictionary(entry => entry.id, entry => entry);
            }

            private int PageCount()
            {
                if (_inventory == null)
                {
                    return 0;
                }

                int fullPages = _inventory.Count / ITEMS_PER_PAGE;
                int remainingPages = _inventory.Count % ITEMS_PER_PAGE == 0 ? 0 : 1;

                return fullPages + remainingPages;
            }

            private bool HasNextPage()
            {
                return _currentPageId < PageCount() - 1;
            }

            private bool HasPreviousPage()
            {
                return _currentPageId > 0;
            }

            private void Awake()
            {
                _player = GetComponent<BasePlayer>();
                if (_player == null)
                {
                    throw new Exception(
                        "Component added to an Object without BasePlayer behaviour"
                    );
                }
            }

            private List<APIClient.InventoryEntryDto> CurrentPageItems()
            {
                return _inventory
                    .Skip(ITEMS_PER_PAGE * _currentPageId)
                    .Take(ITEMS_PER_PAGE)
                    .ToList();
            }

            private void OnDestroy()
            {
                CloseAndReleaseInventory();
            }

            public void gMonetize_OpenUI()
            {
                if (_isOpen)
                    return;

                CuiHelper.AddUi(_player, Builder.Base());
                DrawPaginationButtons(true);
                _isOpen = true;
            }

            public void gMonetize_LoadingItems() =>
                DrawNotification(NotificationState.LoadingItems);

            public void gMonetize_CloseUI()
            {
                CloseAndReleaseInventory();
            }

            public void gMonetize_RedeemItemOk(string inventoryEntryId) { }

            public void gMonetize_ItemClaimedError(object[] args)
            {
                string inventoryEntryId = (string)args[0];
                int errorCode = (int)args[1];
            }

            public void gMonetize_InventoryReceived(
                List<APIClient.InventoryEntryDto> inventoryEntries
            )
            {
                _currentPageId = 0;

                if (!_isOpen)
                    return;

                if (!inventoryEntries.Any())
                {
                    DrawNotification(NotificationState.InventoryEmpty);
                    return;
                }

                _inventory = inventoryEntries;
                RemoveNotification();
                DrawInventoryPage();
                DrawPaginationButtons();
            }

            public void gMonetize_InventoryReceiveFail(int errorCode)
            {
                if (!_isOpen)
                    return;

                switch (errorCode)
                {
                    case 404:
                        DrawNotification(NotificationState.CustomerNotFound);
                        break;

                    case 401:
                        DrawNotification(NotificationState.Unauthorized);
                        break;

                    default:
                        DrawNotification(NotificationState.UnknownError);
                        break;
                }
            }

            public void gMonetize_PreviousPage()
            {
                if (!HasPreviousPage() || !_isOpen)
                {
                    return;
                }

                RemoveCurrentPageItems();
                _currentPageId--;
                DrawInventoryPage();
                DrawPaginationButtons();
            }

            public void gMonetize_NextPage()
            {
                if (!HasNextPage() || !_isOpen)
                {
                    return;
                }

                RemoveCurrentPageItems();
                _currentPageId--;
                DrawInventoryPage();
                DrawPaginationButtons();
            }

            public void gMonetize_RedeemItemPending(string id)
            {
                if (!_isOpen)
                    return;

                APIClient.InventoryEntryDto entry;
                if (!_inventoryIdMap.TryGetValue(id, out entry))
                {
                    LogDebug(
                        "Called gMonetize_RedeemItemPending({0}), but item was not found in inventory",
                        id
                    );
                    return;
                }
            }

            private void CloseAndReleaseInventory()
            {
                if (_isOpen)
                {
                    CuiHelper.DestroyUi(_player, Names.MAIN_BACKGROUND);
                    _isOpen = false;
                }

                _isItemListContainerDrawn = false;
                _notificationState = NotificationState.None;
                _inventory = null;
                _currentPageId = 0;
            }

            private void RemoveCurrentPageItems()
            {
                foreach (
                    Names.ItemCardName cardName in CurrentPageItems()
                        .Select(item => Names.ItemCard(item.id))
                )
                {
                    CuiHelper.DestroyUi(_player, cardName.Container);
                }
            }

            private void DrawPaginationButtons(bool firstTime = false)
            {
                if (!firstTime)
                {
                    CuiHelper.DestroyUi(_player, Names.PAGINATION_BTN_PREV);
                    CuiHelper.DestroyUi(_player, Names.PAGINATION_BTN_NEXT);
                }

                CuiHelper.AddUi(
                    _player,
                    Builder.PaginationButtons(HasPreviousPage(), HasNextPage()).ToList()
                );
            }

            private void DrawItemListContainer()
            {
                if (!_isOpen)
                {
                    LogDebug("Calling DrawItemListContainer with _isOpen == false");
                    return;
                }

                if (_isItemListContainerDrawn)
                {
                    // no warn here, bc it is normal to call this method to just ensure it's drawn
                    return;
                }

                CuiHelper.AddUi(_player, new List<CuiElement> { Builder.ItemListContainer() });
                _isItemListContainerDrawn = true;
            }

            private void RemoveItemListContainer()
            {
                if (!_isOpen || !_isItemListContainerDrawn)
                {
                    return;
                }

                CuiHelper.DestroyUi(_player, Names.ITEMLIST_CONTAINER);
                _isItemListContainerDrawn = false;
            }

            private void DrawInventoryPage()
            {
                DrawItemListContainer();

                List<APIClient.InventoryEntryDto> itemList = CurrentPageItems();
                List<CuiElement> componentList = new List<CuiElement>();
                for (int i = 0; i < itemList.Count; i++)
                {
                    APIClient.InventoryEntryDto item = itemList[i];
                    bool canRedeem = Instance.CanRedeemItem(_player, item);
                    IEnumerable<CuiElement> card = RenderItemCard(i, item, canRedeem);
                    componentList.AddRange(card);
                }

                LogDebug(
                    "DrawInventoryPage. Itemcount: {0}, Component count: {1}",
                    itemList.Count,
                    componentList.Count
                );

                LogDebug("Components json:\n{0}", CuiHelper.ToJson(componentList));

                CuiHelper.AddUi(_player, componentList);
            }

            private IEnumerable<CuiElement> RenderItemCard(
                int containerIndex,
                APIClient.InventoryEntryDto item,
                bool canRedeem
            )
            {
                ICuiComponent icon;

                int? amount = null;

                if (item.type == APIClient.InventoryEntryDto.InventoryEntryType.ITEM)
                {
                    amount = item.item.amount;
                }

                if (item.iconId != null)
                {
                    icon = new CuiRawImageComponent { Url = APIClient.GetIconUrl(item.iconId) };
                }
                else if (item.type == APIClient.InventoryEntryDto.InventoryEntryType.ITEM)
                {
                    icon = new CuiImageComponent { ItemId = item.item.itemId };
                }
                else
                {
                    icon = new CuiRawImageComponent { Url = Icons.ITEM_DEFAULT };
                }

                IEnumerable<CuiElement> card = Builder.ItemCard(
                    containerIndex,
                    item.id,
                    item.name,
                    amount,
                    icon,
                    canRedeem
                );

                return card;
            }

            #region Notifications

            private void DrawNotification(NotificationState state)
            {
                if (!_isOpen)
                {
                    LogDebug("Calling DrawNotification({0:G}), but _isOpen == false", state);

                    return;
                }

                if (_notificationState == state)
                {
                    return;
                }

                if (_notificationState != NotificationState.None)
                {
                    RemoveNotification();
                }

                switch (state)
                {
                    case NotificationState.Unauthorized:
                        CuiHelper.AddUi(
                            _player,
                            Builder
                                .Notification(
                                    "Invalid API key",
                                    "It's not your fault. Let the admins know!",
                                    Icons.CLOSE_CROSS
                                )
                                .ToList()
                        );
                        break;
                    case NotificationState.LoadingItems:
                        CuiHelper.AddUi(
                            _player,
                            Builder
                                .Notification(
                                    "Loading items",
                                    "Receiving what your mom has paid for...",
                                    Icons.NOTIFICATION_PENDING
                                )
                                .ToList()
                        );
                        break;
                    case NotificationState.CustomerNotFound:
                        CuiHelper.AddUi(
                            _player,
                            Builder
                                .Notification(
                                    "Failed to get inventory",
                                    "You need to be registered in the store",
                                    Icons.USER_NOT_FOUND
                                )
                                .ToList()
                        );
                        break;

                    case NotificationState.UnknownError:
                        CuiHelper.AddUi(
                            _player,
                            Builder
                                .Notification(
                                    "Unknown error",
                                    "Houston, we got problems...",
                                    Icons.CLOSE_CROSS
                                )
                                .ToList()
                        );
                        break;

                    case NotificationState.InventoryEmpty:
                        CuiHelper.AddUi(
                            _player,
                            Builder
                                .Notification(
                                    "No items",
                                    "Looks like your inventory is empty...",
                                    Icons.NOTIFICATION_INVENTORY_EMPTY
                                )
                                .ToList()
                        );
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(state), state, null);
                }

                _notificationState = state;
            }

            private void RemoveNotification()
            {
                if (!_isOpen || _notificationState == NotificationState.None)
                {
                    return;
                }

                CuiHelper.DestroyUi(_player, Names.NOTIFICATION_CONTAINER);
            }

            #endregion


            private enum NotificationState
            {
                None,
                Unauthorized,
                LoadingItems,
                CustomerNotFound,
                UnknownError,
                InventoryEmpty
            }

            private static class Builder
            {
                public const int COLUMNS = 8;
                public const int ROWS = 4;
                public const int CARD_COUNT = COLUMNS * ROWS;
                public const float COLUMN_GAP = .005f;
                public const float ROW_GAP = .01f;

                public static string Base()
                {
                    List<CuiElement> components = new List<CuiElement>
                    {
                        new CuiElement
                        {
                            Parent = "Hud.Menu",
                            Name = Names.MAIN_BACKGROUND,
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Material = Materials.BACKGROUND_BLUR_INGAMEMENU,
                                    Color = "0.3 0.3 0.3 0.5"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "1 1"
                                }
                            }
                        },
                        new CuiElement
                        {
                            Parent = Names.MAIN_BACKGROUND,
                            Name = Names.MAIN_BACKGROUND_OVERLAY,
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0.8" },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "1 1"
                                }
                            }
                        },
                        new CuiElement
                        {
                            Parent = Names.MAIN_BACKGROUND,
                            Name = Names.MAIN_CONTAINER,
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.1 0.12",
                                    AnchorMax = "0.9 0.9"
                                }
                            }
                        },
                        new CuiElement
                        {
                            Parent = Names.MAIN_CONTAINER,
                            Name = Names.HEADER_CONTAINER,
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0.955",
                                    AnchorMax = "1 1"
                                }
                            }
                        },
                        new CuiElement
                        {
                            Parent = Names.HEADER_CONTAINER,
                            Name = Names.TITLE_BACKGROUND,
                            Components =
                            {
                                new CuiImageComponent { Color = "0.5 0.5 0.5 0.7" },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.067 0",
                                    AnchorMax = "0.965 1"
                                }
                            }
                        },
                        new CuiElement
                        {
                            Parent = Names.TITLE_BACKGROUND,
                            Name = Names.TITLE_TEXT,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "gMonetize",
                                    Align = TextAnchor.MiddleCenter,
                                    Color = "0.7 0.7 0.7 0.8"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "1 1"
                                }
                            }
                        },
                        new CuiElement
                        {
                            Parent = Names.HEADER_CONTAINER,
                            Name = Names.PAGINATION_BTN_CONTAINER,
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "0.06 1"
                                }
                            }
                        },
                        new CuiElement
                        {
                            Parent = Names.HEADER_CONTAINER,
                            Name = Names.CLOSE_BTN,
                            Components =
                            {
                                new CuiButtonComponent
                                {
                                    Color = "0.7 0.4 0.4 0.8",
                                    Command = CMD_CLOSE
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.97 0",
                                    AnchorMax = "1 1"
                                }
                            }
                        },
                        new CuiElement
                        {
                            Parent = Names.CLOSE_BTN,
                            Name = Names.CLOSE_BTN_ICON,
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Url = Icons.CLOSE_CROSS,
                                    Color = "1 1 1 0.6"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.2 0.2",
                                    AnchorMax = "0.8 0.8"
                                }
                            }
                        }
                    };

                    return CuiHelper.ToJson(components);
                }

                public static IEnumerable<CuiElement> PaginationButtons(bool bPrev, bool bNext)
                {
                    const string COLOR_ENABLED = "0.5 0.5 0.5 0.7";
                    const string COLOR_ICON_ENABLED = "0.8 0.8 0.8 0.6";

                    const string COLOR_DISABLED = "0.5 0.5 0.5 0.5";
                    const string COLOR_ICON_DISABLED = "0.6 0.6 0.6 0.5";

                    return new[]
                    {
                        new CuiElement
                        {
                            Parent = Names.PAGINATION_BTN_CONTAINER,
                            Name = Names.PAGINATION_BTN_PREV,
                            Components =
                            {
                                new CuiButtonComponent
                                {
                                    Color = bPrev ? COLOR_ENABLED : COLOR_DISABLED
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "0.475 0.97"
                                }
                            }
                        },
                        new CuiElement
                        {
                            Parent = Names.PAGINATION_BTN_PREV,
                            Name = Names.PAGINATION_BTN_PREV_ICON,
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Url = Icons.ARROW_LEFT,
                                    Color = bPrev ? COLOR_ICON_ENABLED : COLOR_ICON_DISABLED
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.2 0.2",
                                    AnchorMax = "0.8 0.8"
                                }
                            }
                        },
                        new CuiElement
                        {
                            Parent = Names.PAGINATION_BTN_CONTAINER,
                            Name = Names.PAGINATION_BTN_NEXT,
                            Components =
                            {
                                new CuiButtonComponent
                                {
                                    Color = bNext ? COLOR_ENABLED : COLOR_DISABLED
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.525 0",
                                    AnchorMax = "1 0.97"
                                }
                            }
                        },
                        new CuiElement
                        {
                            Parent = Names.PAGINATION_BTN_NEXT,
                            Name = Names.PAGINATION_BTN_NEXT_ICON,
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Url = Icons.ARROW_RIGHT,
                                    Color = bNext ? COLOR_ICON_ENABLED : COLOR_ICON_DISABLED
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.2 0.2",
                                    AnchorMax = "0.8 0.8"
                                }
                            }
                        }
                    };
                }

                public static IEnumerable<CuiElement> Notification(
                    string title,
                    string text,
                    string iconUrl,
                    string btnCommand = null
                )
                {
                    List<CuiElement> components = new List<CuiElement>
                    {
                        new CuiElement
                        {
                            Parent = Names.MAIN_CONTAINER,
                            Name = Names.NOTIFICATION_CONTAINER,
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.415 0.475",
                                    AnchorMax = "0.715 0.525"
                                }
                            }
                        },
                        new CuiElement
                        {
                            Parent = Names.NOTIFICATION_CONTAINER,
                            Name = Names.NOTIFICATION_ICON,
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Url = iconUrl, //,
                                    Color = "1 1 1 0.6"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "0.1 1"
                                }
                            }
                        },
                        new CuiElement
                        {
                            Parent = Names.NOTIFICATION_CONTAINER,
                            Name = Names.NOTIFICATION_TITLE,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = title,
                                    FontSize = 10,
                                    Align = TextAnchor.LowerLeft
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.12 0.5",
                                    AnchorMax = "1 1"
                                }
                            }
                        },
                        new CuiElement
                        {
                            Parent = Names.NOTIFICATION_CONTAINER,
                            Name = Names.NOTIFICATION_MESSAGE,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = text,
                                    FontSize = 8,
                                    Align = TextAnchor.MiddleLeft,
                                    Color = "0.8 0.8 0.8 0.8"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.12 0",
                                    AnchorMax = "1 0.6"
                                }
                            }
                        }
                    };

                    if (btnCommand != null)
                    {
                        components.Add(
                            new CuiElement
                            {
                                Parent = Names.NOTIFICATION_CONTAINER,
                                Name = Names.NOTIFICATION_BTN,
                                Components =
                                {
                                    new CuiButtonComponent
                                    {
                                        Color = "0 0 0 0",
                                        Command = btnCommand
                                    },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0 0",
                                        AnchorMax = "1 1"
                                    }
                                }
                            }
                        );
                    }

                    return components;
                }

                public static CuiElement ItemListContainer()
                {
                    return new CuiElement
                    {
                        Parent = Names.MAIN_CONTAINER,
                        Name = Names.ITEMLIST_CONTAINER,
                        Components =
                        {
                            new CuiImageComponent { Color = "0 0 0 0" },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 0.945"
                            }
                        }
                    };
                }

                public static IEnumerable<CuiElement> ItemCard(
                    int containerIndex,
                    string id,
                    string name,
                    int? amount,
                    ICuiComponent icon,
                    bool canRedeem
                )
                {
                    Names.ItemCardName uiName = Names.ItemCard(id);

                    List<CuiElement> components = new List<CuiElement>
                    {
                        new CuiElement
                        {
                            Parent = Names.ITEMLIST_CONTAINER,
                            Name = uiName.Container,
                            Components =
                            {
                                new CuiImageComponent { Color = "0.5 0.5 0.5 0.7" },
                                Utilities.GridTransform(
                                    containerIndex,
                                    COLUMNS,
                                    ROWS,
                                    COLUMN_GAP,
                                    ROW_GAP
                                )
                            }
                        },
                        new CuiElement
                        {
                            Parent = uiName.Container,
                            Name = uiName.FooterContainer,
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.05 0.05",
                                    AnchorMax = "0.95 0.25"
                                }
                            }
                        },
                        new CuiElement
                        {
                            Parent = uiName.FooterContainer,
                            Name = uiName.TextContainer,
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "0.5 1"
                                }
                            }
                        },
                        new CuiElement
                        {
                            Parent = uiName.TextContainer,
                            Name = uiName.NameText,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = name,
                                    FontSize = 12,
                                    Font = Fonts.ROBOTOCONDENSED_REGULAR,
                                    Align = TextAnchor.MiddleLeft
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0.5",
                                    AnchorMax = "1 1"
                                }
                            }
                        },
                        new CuiElement
                        {
                            Parent = uiName.Container,
                            Name = uiName.Icon,
                            Components =
                            {
                                icon,
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.04 0.3",
                                    AnchorMax = "0.946 0.95"
                                }
                            }
                        }
                    };

                    if (amount != null)
                    {
                        components.Add(
                            new CuiElement
                            {
                                Parent = uiName.TextContainer,
                                Name = uiName.AmountText,
                                Components =
                                {
                                    new CuiTextComponent
                                    {
                                        Text = 'x' + amount.ToString(),
                                        FontSize = 10,
                                        Color = "0.7 0.7 0.7 0.8",
                                        Align = TextAnchor.LowerLeft
                                    },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0 0",
                                        AnchorMax = "1 0.5"
                                    }
                                }
                            }
                        );
                    }

                    string btnColor,
                        btnIconUrl,
                        btnText,
                        btnIconColor,
                        btnTextColor;

                    if (canRedeem)
                    {
                        btnColor = "0.5 0.65 0.5 0.7";
                        btnTextColor = "0.7 0.85 0.7 0.85";
                        btnIconColor = "0.7 0.85 0.7 0.85";
                        btnIconUrl = Icons.REDEEM;
                        btnText = "Redeem";
                    }
                    else
                    {
                        btnColor = "0.5 0.5 0.5 0.7";
                        btnTextColor = "0.65 0.65 0.65 0.85";
                        btnIconColor = "0.6 0.6 0.6 0.85";
                        btnIconUrl = "https://i.imgur.com/xEwbjZ0.png";
                        btnText = "No\nspace";
                    }

                    components.AddRange(
                        new[]
                        {
                            new CuiElement
                            {
                                Parent = uiName.FooterContainer,
                                Name = uiName.Btn,
                                Components =
                                {
                                    new CuiButtonComponent { Color = btnColor },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5 0",
                                        AnchorMax = "0.99 1"
                                    }
                                }
                            },
                            new CuiElement
                            {
                                Parent = uiName.Btn,
                                Name = uiName.BtnText,
                                Components =
                                {
                                    new CuiTextComponent
                                    {
                                        Text = btnText,
                                        FontSize = 10,
                                        Align = TextAnchor.MiddleCenter,
                                        Color = btnTextColor,
                                        Font = Fonts.ROBOTOCONDENSED_REGULAR
                                    },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.25 0",
                                        AnchorMax = "1 1"
                                    }
                                }
                            },
                            new CuiElement
                            {
                                Parent = uiName.Btn,
                                Name = uiName.BtnIcon,
                                Components =
                                {
                                    new CuiRawImageComponent
                                    {
                                        Url = btnIconUrl,
                                        Color = btnIconColor,
                                    },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.02 0.28",
                                        AnchorMax = "0.3 0.74"
                                    }
                                }
                            }
                        }
                    );

                    return components;
                }
            }

            private static class Utilities
            {
                public static CuiRectTransformComponent GridTransform(
                    int containerIndex,
                    int columnCount,
                    int rowCount,
                    float columnGap,
                    float rowGap
                )
                {
                    int columnGapCount = columnCount - 1;
                    int rowGapCount = rowCount - 1;

                    float totalColumnGap = columnGap * columnGapCount;
                    float totalRowGap = rowGap * rowGapCount;

                    float cardWidth = (1.0f - totalColumnGap) / columnCount;
                    float cardHeight = (1.0f - totalRowGap) / rowCount;

                    int columnIndex = containerIndex % columnCount;
                    int rowIndex = containerIndex / columnCount;

                    float sumColumnGap = columnGap * columnIndex;
                    float sumRowGap = rowGap * rowIndex;

                    float sumPrevCardWidth = cardWidth * columnIndex;
                    float sumPrevCardHeight = cardHeight * (rowIndex + 1);

                    float offsetX = sumColumnGap + sumPrevCardWidth;
                    float offsetY = 1.0f - (sumRowGap + sumPrevCardHeight);

                    return new CuiRectTransformComponent
                    {
                        AnchorMin = $"{offsetX} {offsetY}",
                        AnchorMax = $"{offsetX + cardWidth} {offsetY + cardHeight}"
                    };
                }
            }

            private struct ItemCard
            {
                public readonly string Id;
                public readonly int ContainerIndex;
                public readonly string Name;
                public readonly int? Amount;
                public readonly RedeemButtonState ButtonState;
                public readonly string IconURL;
                public readonly Names.ItemCardName UIName;
                public readonly TimeSpan WipeBlockTimeLeft;

                public ItemCard(
                    string id,
                    int containerIndex,
                    string name,
                    int? amount,
                    RedeemButtonState buttonState,
                    string iconURL,
                    TimeSpan wipeBlockTimeLeft
                )
                {
                    Id = id;
                    ContainerIndex = containerIndex;
                    Name = name;
                    Amount = amount;
                    ButtonState = buttonState;
                    IconURL = iconURL;
                    UIName = new Names.ItemCardName(id);
                    WipeBlockTimeLeft = wipeBlockTimeLeft;
                }

                public static ItemCard FromInventoryEntry(
                    int containerIndex,
                    APIClient.InventoryEntryDto inventoryEntry
                ) { }

                public void ToUpdateComponents(ItemCard oldState, List<CuiElement> components)
                {
                    if (ContainerIndex != oldState.ContainerIndex)
                    {
                        components.Add(
                            new CuiElement
                            {
                                Name = UIName.Container,
                                Components =
                                {
                                    Utilities.GridTransform(
                                        ContainerIndex,
                                        Builder.COLUMNS,
                                        Builder.ROWS,
                                        Builder.COLUMN_GAP,
                                        Builder.ROW_GAP
                                    )
                                }
                            }
                        );
                    }

                    if (ButtonState != oldState.ButtonState)
                    {
                        AddButton(components);
                    }
                }

                private void AddButton(List<CuiElement> components)
                {
                    throw new NotImplementedException();
                    
                    switch (ButtonState)
                    {
                        case RedeemButtonState.CAN_REDEEM:
                            break;
                        case RedeemButtonState.NO_SPACE:
                            break;
                        case RedeemButtonState.FAILED:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                public void ToComponents(List<CuiElement> components)
                {
                    components.Clear();
                }

                public enum RedeemButtonState
                {
                    CAN_REDEEM,
                    NO_SPACE,
                    FAILED
                }
            }

            private static class Names
            {
                public const string MAIN_BACKGROUND = "gmonetize/main/background";
                public const string MAIN_BACKGROUND_OVERLAY = "gmonetize/main/background/overlay";
                public const string MAIN_CONTAINER = "gmonetize/main/container";
                public const string HEADER_CONTAINER = "gmonetize/header/container";
                public const string TITLE_BACKGROUND = "gmonetize/header/title/background";
                public const string TITLE_TEXT = "gmonetize/header/title/text";
                public const string PAGINATION_BTN_CONTAINER = "gmonetize/pagination-btn/container";
                public const string PAGINATION_BTN_NEXT = "gmonetize/pagination-btn/next";
                public const string PAGINATION_BTN_NEXT_ICON = "gmonetize/pagination-btn/next/icon";
                public const string PAGINATION_BTN_PREV = "gmonetize/pagination-btn/prev";
                public const string PAGINATION_BTN_PREV_ICON = "gmonetize/pagination-btn/prev/icon";
                public const string CLOSE_BTN = "gmonetize/close-btn";
                public const string CLOSE_BTN_ICON = "gmonetize/close-btn/icon";
                public const string ITEMLIST_CONTAINER = "gmonetize/itemlist/container";
                public const string NOTIFICATION_CONTAINER = "gmonetize/notification/container";
                public const string NOTIFICATION_ICON = "gmonetize/notification/icon";
                public const string NOTIFICATION_TITLE = "gmonetize/notification/title";
                public const string NOTIFICATION_MESSAGE = "gmonetize/notification/message";
                public const string NOTIFICATION_BTN = "gmonetize/notification/btn";
                public const string NOTIFICATION_BTN_TEXT = "gmonetize/notification/btn/text";

                /*public const string NOTIFICATION_TITLE_BACKGROUND = "gmonetize/notification/title/background";
                public const string NOTIFICATION_TITLE_TEXT = "gmonetize/notification/title/text";
                public const string NOTIFICATION_MESSAGE = "gmonetize/notification/message";
                public const string NOTIFICATION_BTN = "gmonetize/notification/btn";
                public const string NOTIFICATION_BTN_TEXT = "gmonetize/notification/btn/text";*/

                public static ItemCardName ItemCard(string id)
                {
                    return new ItemCardName(id);
                }

                public struct ItemCardName
                {
                    public readonly string Container;
                    public readonly string Icon;
                    public readonly string FooterContainer;
                    public readonly string TextContainer;
                    public readonly string NameText;
                    public readonly string AmountText;
                    public readonly string Btn;
                    public readonly string BtnIcon;
                    public readonly string BtnText;

                    public ItemCardName(string id)
                    {
                        Container = Base(id) + "container";
                        Icon = Base(id) + "icon";
                        FooterContainer = Base(id) + "footer/container";
                        TextContainer = Base(id) + "footer/text-container";
                        NameText = TextContainer + "/text/name";
                        AmountText = TextContainer + "/text/amount";
                        Btn = Base(id) + "footer/btn";
                        BtnIcon = Btn + "/icon";
                        BtnText = Btn + "/text";
                    }

                    private static string Base(string id)
                    {
                        return $"gmonetize/itemcard({id})/";
                    }
                }
            }

            private static class Materials
            {
                public const string BACKGROUND_BLUR_INGAMEMENU =
                    "assets/content/ui/uibackgroundblur-ingamemenu.mat";
            }

            private static class Fonts
            {
                public const string ROBOTOCONDENSED_REGULAR = "robotocondensed-regular.ttf";
                public const string ROBOTOCONDENSED_BOLD = "robotocondensed.ttf";
            }

            private static class Icons
            {
                public const string NOTIFICATION_ERROR = "";
                public const string NOTIFICATION_RETRY = "";
                public const string NOTIFICATION_PENDING = "";
                public const string CLOSE_CROSS = "https://i.imgur.com/zeCzK3i.png";
                public const string ARROW_LEFT = "https://i.imgur.com/TiYyODy.png";
                public const string ARROW_RIGHT = "https://i.imgur.com/tBYlfGM.png";
                public const string ITEM_DEFAULT =
                    "https://api.gmonetize.ru/static/v2/image/plugin/icons/rust_94773.png";
                public const string REDEEM = "https://i.imgur.com/xEwbjZ0.png";
                public const string REDEEM_RETRY = "";
                public const string REDEEM_WIPEBLOCKED = "";
                public const string REDEEM_NOSPACE = "";
                public const string USER_NOT_FOUND = "";
                public const string NOTIFICATION_INVENTORY_EMPTY = "";
            }
        }

        private static class APIClient
        {
            private static Dictionary<string, string> Headers =>
                new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" },
                    { "Authorization", $"ApiKey {Instance._settings.ApiKey}" }
                };

            private static string MainBaseUrl => Instance._settings.ApiUrl + "/main/v3/plugin";
            private static string StaticBaseUrl => Instance._settings.ApiUrl + "/static/v2";

            public static void GetPlayerInventory(
                string userId,
                Action<List<InventoryEntryDto>> okCb,
                Action<int> errorCb
            )
            {
                string url = MainBaseUrl + $"/customer/STEAM/{userId}/inventory";

                Instance.webrequest.Enqueue(
                    url,
                    null,
                    (code, body) =>
                    {
                        if (code == 200)
                        {
                            LogDebug("Received inventory:\n{0}", body);
                            okCb(JsonConvert.DeserializeObject<List<InventoryEntryDto>>(body));
                        }
                        else
                        {
                            errorCb(code);
                        }
                    },
                    Instance,
                    RequestMethod.GET,
                    Headers
                );
            }

            public static void RedeemItem(
                string userId,
                string inventoryEntryId,
                Action okCb,
                Action<int> errorCb
            )
            {
                string url =
                    MainBaseUrl + $"/customer/STEAM/{userId}/inventory/{inventoryEntryId}/redeem";

                Instance.webrequest.Enqueue(
                    url,
                    null,
                    (code, body) =>
                    {
                        if (code == 204)
                        {
                            okCb();
                        }
                        else
                        {
                            errorCb(code);
                        }
                    },
                    Instance,
                    RequestMethod.POST,
                    Headers
                );
            }

            public static string GetIconUrl(string iconId)
            {
                return StaticBaseUrl + $"/image/{iconId}";
            }

            public class InventoryEntryDto
            {
                public string id;
                public InventoryEntryType type;
                public string name;
                public string iconId;

                [JsonConverter(typeof(DurationTimeSpanJsonConverter))]
                public TimeSpan? wipeBlockDuration;

                public ItemDto item;
                public ResearchDto research;
                public RankDto rank;

                [JsonConverter(typeof(StringEnumConverter))]
                public enum InventoryEntryType
                {
                    ITEM,
                    KIT,
                    RANK,
                    RESEARCH,
                    CUSTOM,
                    PERMISSION
                }
            }

            public class ItemDto
            {
                public string id;
                public string name;
                public int itemId;
                public int amount;
                public ItemMetaDto meta;
                public string iconId;

                public class ItemMetaDto
                {
                    public ulong? skinId;
                    public float condition;
                }
            }

            public class ResearchDto
            {
                public string id;
                public string name;
                public int researchId;
                public string iconId;
            }

            public class RankDto
            {
                public string id;
                public string name;
                public string groupName;

                [JsonConverter(typeof(DurationTimeSpanJsonConverter))]
                public TimeSpan? duration;
                public string iconId;
            }

            private class DurationTimeSpanJsonConverter : JsonConverter
            {
                public override void WriteJson(
                    JsonWriter writer,
                    object value,
                    JsonSerializer serializer
                )
                {
                    throw new NotImplementedException();
                }

                public override object ReadJson(
                    JsonReader reader,
                    Type objectType,
                    object existingValue,
                    JsonSerializer serializer
                )
                {
                    string stringValue = reader.Value as string;

                    if (string.IsNullOrEmpty(stringValue))
                    {
                        LogDebug("TimeSpan stringValue is null");
                        return null;
                    }

                    TimeSpan ts = ParseDuration(stringValue);

                    LogDebug("Converting to timeSpan: {0}=>{1}", stringValue, ts);

                    return ts;
                }

                public override bool CanConvert(Type objectType)
                {
                    return objectType == typeof(TimeSpan?);
                }

                public static TimeSpan ParseDuration(string input)
                {
                    int tIndex = input.IndexOf('T');

                    int timePortionIndex = tIndex != -1 ? tIndex + 1 : 0;

                    int partStart = timePortionIndex;
                    int hours = 0,
                        minutes = 0,
                        seconds = 0;
                    for (int i = timePortionIndex; i < input.Length; i++)
                    {
                        char c = input[i];

                        string partBuf;

                        switch (c)
                        {
                            case 'H':
                                partBuf = input.Substring(partStart, i - partStart);
                                hours = int.Parse(partBuf);
                                partStart = i + 1;
                                break;
                            case 'M':
                                partBuf = input.Substring(partStart, i - partStart);
                                minutes = int.Parse(partBuf);
                                partStart = i + 1;
                                break;
                            case 'S':
                            case 'Z':
                                partBuf = input.Substring(partStart, i - partStart);
                                seconds = int.Parse(partBuf);
                                partStart = i + 1;
                                if (c == 'Z')
                                {
                                    i = input.Length;
                                }

                                break;

                            default:
                                if (i == input.Length - 1)
                                {
                                    partBuf = input.Substring(partStart, i - partStart + 1);
                                    seconds = int.Parse(partBuf);
                                }

                                break;
                        }
                    }

                    return new TimeSpan(0, hours, minutes, seconds);
                }
            }
        }

        private class PluginSettings
        {
            public string ApiKey { get; set; }
            public string ApiUrl { get; set; }
            public string[] ChatCommands { get; set; }
            public PermissionGiveMethod PermissionGiveKind { get; set; }

            public static PluginSettings GetDefaults()
            {
                return new PluginSettings
                {
                    ApiKey = "change me",
                    ApiUrl = "https://api.gmonetize.ru",
                    ChatCommands = new[] { "shop" },
                    PermissionGiveKind = PermissionGiveMethod.Auto
                };
            }

            public enum PermissionGiveMethod
            {
                Auto,
                Internal,
                TimedPermissions,
                IQPermissions
            }
        }
    }
}
