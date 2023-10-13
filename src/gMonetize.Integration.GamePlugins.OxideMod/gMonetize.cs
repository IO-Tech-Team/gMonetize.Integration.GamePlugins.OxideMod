#define DEBUG

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using UnityEngine;
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

namespace Oxide.Plugins
{
    [Info("gMonetize", "gMonetize Project", "2.0.0")]
    [Description("gMonetize integration with OxideMod")]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once ClassNeverInstantiated.Global
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
        private IPermissionsIntegrationModule _permissionsIntegrationModule;

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
                APIClient.Init(_settings.ApiUrl, _settings.ApiKey);
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

        #region Lang API

        private void SendChatMessage(IPlayer player, string key)
        {
            string prefix = TranslatePrefix(player);
            string message = Translate(key, player);

            player.Message(prefix + message);
        }

        private void SendChatMessage(IPlayer player, string key, object arg0)
        {
            string prefix = TranslatePrefix(player);
            string format = Translate(key, player);

            player.Message(prefix + string.Format(format, arg0));
        }

        private void SendChatMessage(IPlayer player, string key, object arg0, object arg1)
        {
            string prefix = TranslatePrefix(player);
            string format = Translate(key, player);

            player.Message(prefix + string.Format(format, arg0, arg1));
        }

        private void SendChatMessage(
            IPlayer player,
            string key,
            object arg0,
            object arg1,
            object arg2
        )
        {
            string prefix = TranslatePrefix(player);
            string format = Translate(key, player);

            player.Message(prefix + string.Format(format, arg0, arg1, arg2));
        }

        private void SendChatMessage(IPlayer player, string key, params object[] args)
        {
            string prefix = TranslatePrefix(player);
            string format = Translate(key, player);

            player.Message(prefix + string.Format(format, args));
        }

        private string TranslatePrefix(IPlayer player)
        {
            return Translate("chat.prefix", player);
        }

        private string Translate(string key, IPlayer player = null)
        {
            return lang.GetMessage(key, this, player?.Id);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(
                new Dictionary<string, string>
                {
                    ["chat.prefix"] = "[gMonetize] ",
                    ["chat.error.no_permission"] = "You don't have access to this command",
                    ["ui.header.title"] = "gMonetize",
                    ["ui.notification.loading_items.title"] = "Loading items",
                    ["ui.notification.loading_items.msg"] = "Getting what your mom has paid for...",
                    ["ui.notification.inventory_empty.title"] = "No items",
                    ["ui.notification.inventory_empty.msg"] = "Seems like your inventory is empty",
                    ["ui.notification.user_not_found.title"] = "No user",
                    ["ui.notification.user_not_found.msg"] = "You need to register first",
                    ["ui.notification.error.authorization.title"] = "Invalid API key",
                    ["ui.notification.error.authorization.msg"] =
                        "It's not your fault. Let the admins know!",
                    ["ui.notification.error.generic.title"] = "Unknown error",
                    ["ui.notification.error.generic.msg"] = "Houston, we got problems...",
                    ["ui.itemcard.redeem-btn.text.redeem"] = "Redeem",
                    ["ui.itemcard.redeem-btn.text.no_space"] = "No space",
                    ["ui.itemcard.redeem-btn.text.researched"] = "Already researched",
                    ["ui.itemcard.redeem-btn.text.pending"] = "Redeeming...",
                    ["ui.itemcard.redeem-btn.text.failed"] = "Failed to redeem\nPress to try again"
                },
                this
            );
            lang.RegisterMessages(
                new Dictionary<string, string>
                {
                    ["chat.prefix"] = "[gMonetize] ",
                    ["ui.header.title"] = "gMonetize",
                    ["ui.notification.loading_items.title"] = "Loading items",
                    ["ui.notification.loading_items.msg"] = "Getting what your mom has paid for...",
                    ["ui.notification.inventory_empty.title"] = "No items",
                    ["ui.notification.inventory_empty.msg"] = "Seems like your inventory is empty",
                    ["ui.notification.user_not_found.title"] = "No user",
                    ["ui.notification.user_not_found.msg"] = "You need to register first",
                    ["ui.notification.error.authorization.title"] = "Invalid API key",
                    ["ui.notification.error.authorization.msg"] =
                        "It's not your fault. Let the admins know!",
                    ["ui.notification.error.generic.title"] = "Unknown error",
                    ["ui.notification.error.generic.msg"] = "Houston, we got problems...",
                    ["ui.itemcard.redeem-btn.text.redeem"] = "Redeem",
                    ["ui.itemcard.redeem-btn.text.no_space"] = "No space",
                    ["ui.itemcard.redeem-btn.text.researched"] = "Already researched",
                    ["ui.itemcard.redeem-btn.text.pending"] = "Redeeming...",
                    ["ui.itemcard.redeem-btn.text.failed"] = "Failed to redeem\nPress to try again"
                },
                this,
                "ru"
            );
        }

        #endregion

        #region Oxide hooks

        private void Init()
        {
            Instance = this;
            permission.RegisterPermission(PERMISSION_USE, this);
            RegisterCommands();
        }

        private void OnServerInitialized()
        {
            APIClient.Init(_settings.ApiUrl, _settings.ApiKey);

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
            BasePlayer basePlayer;
            if (TryGetBasePlayer(player, out basePlayer))
            {
                basePlayer.gameObject.AddComponent<UI>();
            }
            else
            {
                LogWarning("OnUserConnected({0}): cannot get BasePlayer", player);
            }
        }

        private void OnUserDisconnected(IPlayer player)
        {
            BasePlayer basePlayer;
            if (TryGetBasePlayer(player, out basePlayer))
            {
                UnityEngine.Object.Destroy(basePlayer.GetComponent<UI>());
            }
            else
            {
                LogWarning("OnUserDisconnected({0}): cannot get BasePlayer", player);
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

        #region Command handling

        private void RegisterCommands()
        {
            covalence.RegisterCommand(CMD_OPEN, this, HandleCommand);
            covalence.RegisterCommand(CMD_CLOSE, this, HandleCommand);
            covalence.RegisterCommand(CMD_NEXT_PAGE, this, HandleCommand);
            covalence.RegisterCommand(CMD_PREV_PAGE, this, HandleCommand);
            covalence.RegisterCommand(CMD_RETRY_LOAD, this, HandleCommand);
            covalence.RegisterCommand(CMD_REDEEM_ITEM, this, HandleCommand);

            foreach (string command in _settings.ChatCommands)
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
            LogDebug(
                "Player {0} has executed command: {1}, [{2}]",
                player,
                command,
                string.Join(", ", args)
            );

            if (command != CMD_CLOSE && !player.HasPermission(PERMISSION_USE))
            {
                SendChatMessage(player, "chat.error.no_permission");
                return true;
            }

            BasePlayer basePlayer;

            if (!TryGetBasePlayer(player, out basePlayer))
            {
                LogError(
                    "BasePlayer not found while calling cmd {0} on player {1}",
                    command,
                    player
                );
                SendChatMessage(player, "chat.error.baseplayer_not_found");
                return true;
            }

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
                        () =>
                        {
                            var component = basePlayer.GetComponent<UI>();
                            var inventoryEntry = component.GetCachedInventoryEntry(args[0]);
                            if (!TryGiveInventoryEntry(basePlayer, inventoryEntry))
                            {
                                component.gMonetize_RedeemItemGiveError(args[0]);
                            }
                            else
                            {
                                component.gMonetize_RedeemItemOk(args[0]);
                            }
                        },
                        code =>
                            basePlayer.SendMessage(
                                nameof(UI.gMonetize_RedeemItemRequestError),
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

        #endregion

        #region Permission helpers

        private void LoadPermissionsIntegrationModule()
        {
            switch (_settings.PermissionGiveKind)
            {
                case PluginSettings.PermissionGiveMethod.Auto:
                    _permissionsIntegrationModule = AutoChoosePermissionsIntegrationModule();
                    break;
                case PluginSettings.PermissionGiveMethod.Internal:
                    _permissionsIntegrationModule = new NativePermissionsIntegration(this);
                    break;
                case PluginSettings.PermissionGiveMethod.TimedPermissions:
                    _permissionsIntegrationModule = new TimedPermissionsIntegration(this);
                    break;
                case PluginSettings.PermissionGiveMethod.IQPermissions:
                    _permissionsIntegrationModule = new IQPermissionsIntegration(this);
                    break;
            }

            LogDebug(
                "Permissions integration module: {0}",
                _permissionsIntegrationModule.GetType().Name
            );
        }

        private IPermissionsIntegrationModule AutoChoosePermissionsIntegrationModule()
        {
            if (plugins.Find("TimedPermissions") != null)
            {
                LogDebug(
                    "Choosing TimedPermissions integration module as IPermissionsIntegrationModule, based on loaded TimedPermissions plugin"
                );
                return new TimedPermissionsIntegration(this);
            }

            if (plugins.Find("IQPermissions") != null)
            {
                LogDebug(
                    "Choosing IQPermissions integration module as IPermissionsIntegrationModule, based on loaded IQPermissions plugin"
                );
                return new IQPermissionsIntegration(this);
            }

            LogDebug(
                "Choosing native permissions integration module as IPermissionsIntegrationModule, because permission plugins were not found"
            );
            return new NativePermissionsIntegration(this);
        }

        #endregion

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

        private bool TryGiveInventoryEntry(
            BasePlayer player,
            APIClient.InventoryEntryDto inventoryEntry
        )
        {
            if (IsWipeBlocked(inventoryEntry))
            {
                return false;
            }

            if (!HasAvailableSpace(player, inventoryEntry))
            {
                return false;
            }

            switch (inventoryEntry.type)
            {
                case APIClient.InventoryEntryDto.InventoryEntryType.ITEM:
                    return TryGiveItemInventoryEntry(player, inventoryEntry);
                case APIClient.InventoryEntryDto.InventoryEntryType.KIT:
                    break;
                case APIClient.InventoryEntryDto.InventoryEntryType.RANK:
                    return TryGiveGroupInventoryEntry(player, inventoryEntry);
                    break;
                case APIClient.InventoryEntryDto.InventoryEntryType.RESEARCH:
                    break;
                case APIClient.InventoryEntryDto.InventoryEntryType.CUSTOM:
                    break;
                case APIClient.InventoryEntryDto.InventoryEntryType.PERMISSION:
                    return TryGivePermissionInventoryEntry(player, inventoryEntry);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return true;
        }

        private bool TryGiveItemInventoryEntry(
            BasePlayer player,
            APIClient.InventoryEntryDto inventoryEntry
        )
        {
            var itemDefinition = ItemManager.FindItemDefinition(inventoryEntry.item.itemId);

            if (!itemDefinition)
            {
                return false;
            }

            if (GetPlayerAvailableSlots(player) < 1)
            {
                return false;
            }

            var item = ItemManager.Create(
                itemDefinition,
                inventoryEntry.item.amount,
                inventoryEntry.item.meta.skinId ?? 0ul
            );

            player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);

            return true;
        }

        private bool TryGivePermissionInventoryEntry(
            BasePlayer player,
            APIClient.InventoryEntryDto inventoryEntry
        )
        {
            if (!inventoryEntry.permission.duration.HasValue)
            {
                _permissionsIntegrationModule.AddPermission(
                    player.IPlayer,
                    inventoryEntry.permission.value
                );
            }
            else
            {
                _permissionsIntegrationModule.AddPermission(
                    player.IPlayer,
                    inventoryEntry.permission.value,
                    inventoryEntry.permission.duration.Value
                );
            }

            return true;
        }

        private bool TryGiveGroupInventoryEntry(
            BasePlayer player,
            APIClient.InventoryEntryDto inventoryEntry
        )
        {
            if (!inventoryEntry.rank.duration.HasValue)
            {
                _permissionsIntegrationModule.AddGroup(
                    player.IPlayer,
                    inventoryEntry.rank.groupName
                );
            }
            else
            {
                _permissionsIntegrationModule.AddGroup(
                    player.IPlayer,
                    inventoryEntry.rank.groupName,
                    inventoryEntry.rank.duration.Value
                );
            }

            return true;
        }

        private bool HasAvailableSpace(
            BasePlayer player,
            APIClient.InventoryEntryDto inventoryEntry
        )
        {
            var slots = GetPlayerAvailableSlots(player);

            if (inventoryEntry.type == APIClient.InventoryEntryDto.InventoryEntryType.ITEM)
            {
                return slots != 0;
            }

            if (
                inventoryEntry.type == APIClient.InventoryEntryDto.InventoryEntryType.KIT
                || inventoryEntry.type == APIClient.InventoryEntryDto.InventoryEntryType.CUSTOM
            )
            {
                return slots
                    >= inventoryEntry.contents.Count(
                        c => c.type == APIClient.GoodObjectDto.GoodObjectType.ITEM
                    );
            }

            return true;
        }

        private bool IsWipeBlocked(APIClient.InventoryEntryDto inventoryEntry)
        {
            if (inventoryEntry.wipeBlockDuration == null)
                return false;

            var timeSinceWipe = DateTime.Now - SaveRestore.SaveCreatedTime;

            return inventoryEntry.wipeBlockDuration < timeSinceWipe;
        }

        private bool IsUnlocked(
            BasePlayer player,
            APIClient.ResearchDto research,
            out ItemDefinition itemDefinition
        )
        {
            return IsUnlocked(player, research.researchId, out itemDefinition);
        }

        private bool IsUnlocked(
            BasePlayer player,
            APIClient.GoodObjectDto goodObject,
            out ItemDefinition itemDefinition
        )
        {
            return IsUnlocked(player, goodObject.researchId, out itemDefinition);
        }

        private bool IsUnlocked(
            BasePlayer player,
            int blueprintId,
            out ItemDefinition itemDefinition
        )
        {
            itemDefinition = ItemManager.FindItemDefinition(blueprintId);

            if (itemDefinition == null)
            {
                LogError("Failed to find ItemDefinition for blueprint ID {0}", blueprintId);
                return false;
            }

            return player.blueprints.IsUnlocked(itemDefinition);
        }

        private class UI : FacepunchBehaviour
        {
            private const int ITEMS_PER_PAGE = Builder.CARD_COUNT;
            private BasePlayer _player;
            private bool _isOpen,
                _isItemListContainerDrawn;
            private NotificationState _notificationState;
            private List<APIClient.InventoryEntryDto> _inventory;
            private List<InventoryCard> _cards;
            private int _currentPageId;

            #region Unity events

            private void Awake()
            {
                _player = GetComponent<BasePlayer>();
                _cards = new List<InventoryCard>();
                if (_player == null)
                {
                    throw new Exception(
                        "Component added to an Object without BasePlayer behaviour"
                    );
                }
            }

            private void OnDestroy()
            {
                CloseAndReleaseInventory();
            }

            #endregion

            #region Pagination helpers

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

            private List<APIClient.InventoryEntryDto> CurrentPageItems()
            {
                return _inventory
                    .Skip(ITEMS_PER_PAGE * _currentPageId)
                    .Take(ITEMS_PER_PAGE)
                    .ToList();
            }

            #endregion

            #region Message handlers

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

            public void gMonetize_RedeemItemGiveError(string id)
            {

                var card = _cards.Find(x => x.EntryId == id);

                if (card==null)
                {
                    throw new Exception("Failed to find rendered card " + id);
                }

                var components = Pool.GetList<CuiElement>();
                if (card.ChangeRedeemState(InventoryEntryRedeemState.FAILED, components))
                {
                    CuiHelper.AddUi(_player, components);
                }
                else
                {
                    LogDebug("Called _RedeemItemGiveError, but state hasn't changed {0}", id);
                }

                Pool.FreeList(ref components);

                /*var elements = Builder.ItemCardButtonUpdate(id, InventoryEntryRedeemState.FAILED);
                CuiHelper.AddUi(_player, elements.ToList());*/
            }

            public void gMonetize_RedeemItemRequestError(object[] args)
            {
                /*string inventoryEntryId = (string)args[0];
                int errorCode = (int)args[1];*/
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
                LogDebug("gMonetize_InventoryReceiveFail({0})", errorCode);
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
                _currentPageId++;
                DrawInventoryPage();
                DrawPaginationButtons();
            }

            public void gMonetize_RedeemItemPending(string id)
            {
                if (!_isOpen)
                    return;

                var card = _cards.Find(x => x.EntryId == id);

                if (card == null)
                {
                    throw new Exception("Failed to find rendered itemcard " + id);
                }

                var components = Pool.GetList<CuiElement>();
                if (card.ChangeRedeemState(InventoryEntryRedeemState.PENDING, components))
                {
                    CuiHelper.AddUi(_player, components);
                }
                else
                {
                    LogDebug("Called _RedeemItemPending, but state hasn't changed ({0})", id);
                }

                Pool.FreeList(ref components);

                /*IEnumerable<CuiElement> elements = Builder.ItemCardButtonUpdate(
                    id,
                    InventoryEntryRedeemState.PENDING
                );

                CuiHelper.AddUi(_player, (List<CuiElement>)elements);*/
            }

            #endregion

            public APIClient.InventoryEntryDto GetCachedInventoryEntry(string id)
            {
                if (_inventory == null)
                {
                    throw new InvalidOperationException("Inventory is null");
                }

                APIClient.InventoryEntryDto entry = _inventory.Find(e => e.id == id);

                if (entry == null)
                {
                    throw new Exception(
                        "Entry with id "
                            + id
                            + " was not found in inventory of player "
                            + _player.IPlayer
                    );
                }

                return entry;
            }

            private void CloseAndReleaseInventory()
            {
                if (_isOpen)
                {
                    CuiHelper.DestroyUi(_player, Names.MAIN_BACKGROUND);
                    _isOpen = false;
                }

                _cards.Clear();
                _isItemListContainerDrawn = false;
                _notificationState = NotificationState.None;
                _inventory = null;
                _currentPageId = 0;
            }

            private void RemoveCurrentPageItems()
            {
                foreach (var card in _cards)
                {
                    var uiName = Names.ItemCard(card.EntryId);
                    CuiHelper.DestroyUi(_player, uiName.Container);
                }

                _cards.Clear();

                /*foreach (
                    Names.ItemCardName cardName in CurrentPageItems()
                        .Select(item => Names.ItemCard(item.id))
                )
                {
                    CuiHelper.DestroyUi(_player, cardName.Container);
                }*/
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

            private bool IsWipeBlocked(APIClient.InventoryEntryDto entry)
            {
                if (entry.wipeBlockDuration == null)
                {
                    return false;
                }

                TimeSpan timeSinceWipe = DateTime.Now - SaveRestore.SaveCreatedTime;

                return entry.wipeBlockDuration > timeSinceWipe;
            }

            private bool IsResearched(APIClient.ResearchDto researchDto)
            {
                ItemDefinition itemDef = ItemManager.FindItemDefinition(researchDto.researchId);

                if (itemDef == null)
                {
                    return false;
                }

                return _player.blueprints.IsUnlocked(itemDef);
            }

            private InventoryEntryRedeemState GetRedeemState(APIClient.InventoryEntryDto entry)
            {
                if (IsWipeBlocked(entry))
                {
                    return InventoryEntryRedeemState.WIPE_BLOCKED;
                }

                if (
                    entry.type == APIClient.InventoryEntryDto.InventoryEntryType.RESEARCH
                    && IsResearched(entry.research)
                )
                {
                    return InventoryEntryRedeemState.NO_SPACE;
                }

                if (
                    entry.type == APIClient.InventoryEntryDto.InventoryEntryType.ITEM
                    && Instance.GetPlayerAvailableSlots(_player) == 0
                )
                {
                    return InventoryEntryRedeemState.NO_SPACE;
                }

                if (entry.type == APIClient.InventoryEntryDto.InventoryEntryType.KIT)
                {
                    if (
                        entry.contents.All(
                            x =>
                                x.type == APIClient.GoodObjectDto.GoodObjectType.RESEARCH
                                && _player.blueprints.IsUnlocked(
                                    ItemManager.FindItemDefinition(x.researchId)
                                )
                        )
                    )
                    {
                        return InventoryEntryRedeemState.NO_SPACE;
                    }

                    int requiredSlots = entry.contents.Count(
                        x => x.type == APIClient.GoodObjectDto.GoodObjectType.ITEM
                    );

                    if (Instance.GetPlayerAvailableSlots(_player) < requiredSlots)
                    {
                        return InventoryEntryRedeemState.NO_SPACE;
                    }
                }

                return InventoryEntryRedeemState.READY;
            }

            private void DrawInventoryPage()
            {
                DrawItemListContainer();

                List<APIClient.InventoryEntryDto> itemList = CurrentPageItems();
                List<CuiElement> componentList = new List<CuiElement>();
                for (int i = 0; i < itemList.Count; i++)
                {
                    APIClient.InventoryEntryDto entry = itemList[i];

                    var card = new InventoryCard(i, entry, GetRedeemState(entry));
                    // IEnumerable<CuiElement> card = RenderItemCard(i, entry, GetRedeemState(entry));
                    // componentList.AddRange();
                    card.Build(componentList);
                    _cards.Add(card);
                }

                LogDebug(
                    "DrawInventoryPage. Itemcount: {0}, Component count: {1}",
                    itemList.Count,
                    componentList.Count
                );

                // LogDebug("Components json:\n{0}", CuiHelper.ToJson(componentList));

                CuiHelper.AddUi(_player, componentList);
            }

            /*private IEnumerable<CuiElement> RenderItemCard(
                int containerIndex,
                APIClient.InventoryEntryDto item,
                InventoryEntryRedeemState redeemState
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
                    icon = new CuiRawImageComponent
                    {
                        Url = APIClient.GetInventoryEntryIconUrl(item.iconId)
                    };
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
                    redeemState
                );

                return card;
            }*/

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

            private class InventoryCard
            {
                public int ContainerIndex;
                public string EntryName;
                public bool HasAmount;
                public int Amount;
                public string EntryId;
                public InventoryEntryRedeemState RedeemState;
                public ICuiComponent IconComponent;

                public InventoryCard(
                    int containerIndex,
                    APIClient.InventoryEntryDto dto,
                    InventoryEntryRedeemState redeemState
                )
                {
                    ContainerIndex = containerIndex;
                    EntryName = dto.name;
                    HasAmount =
                        dto.type == APIClient.InventoryEntryDto.InventoryEntryType.ITEM
                        && dto.item.amount > 1;
                    if (HasAmount)
                        Amount = dto.item.amount;
                    EntryId = dto.id;
                    RedeemState = redeemState;
                    IconComponent = CreateIconComponent(dto);
                }

                public void Build(List<CuiElement> components)
                {
                    var uiName = Names.ItemCard(EntryId);
                    Builder.ItemCardContainer(uiName, ContainerIndex, components, false);
                    Builder.ItemCardStatic(
                        uiName,
                        components,
                        EntryName,
                        IconComponent,
                        HasAmount,
                        Amount
                    );
                    Builder.ItemCardButton(uiName, EntryId, RedeemState, components, false);
                }

                public bool ChangeRedeemState(
                    InventoryEntryRedeemState state,
                    List<CuiElement> components
                )
                {
                    if (RedeemState == state)
                    {
                        return false;
                    }

                    Builder.ItemCardButton(
                        Names.ItemCard(EntryId),
                        EntryId,
                        state,
                        components,
                        true
                    );
                    RedeemState = state;
                    return true;
                }

                public bool ChangeIndex(int containerIndex, List<CuiElement> components)
                {
                    if (containerIndex == ContainerIndex)
                    {
                        return false;
                    }

                    Builder.ItemCardContainer(
                        Names.ItemCard(EntryId),
                        containerIndex,
                        components,
                        true
                    );
                    ContainerIndex = containerIndex;
                    return true;
                }

                private static ICuiComponent CreateIconComponent(APIClient.InventoryEntryDto dto)
                {
                    if (dto.iconId != null)
                    {
                        return new CuiRawImageComponent
                        {
                            Url = APIClient.GetInventoryEntryIconUrl(dto.iconId)
                        };
                    }

                    if (dto.type == APIClient.InventoryEntryDto.InventoryEntryType.ITEM)
                    {
                        return new CuiImageComponent { ItemId = dto.item.itemId };
                    }

                    return new CuiRawImageComponent { Url = Icons.ITEM_DEFAULT };
                }
            }

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
                public const int COLUMNS = 6;
                public const int ROWS = 3;
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
                                new CuiNeedsCursorComponent(),
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
                                    Color = bPrev ? COLOR_ENABLED : COLOR_DISABLED,
                                    Command = CMD_PREV_PAGE
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
                                    Color = bNext ? COLOR_ENABLED : COLOR_DISABLED,
                                    Command = CMD_NEXT_PAGE
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

                public static void ItemCardContainer(
                    Names.ItemCardName uiName,
                    int containerIndex,
                    List<CuiElement> components,
                    bool update
                )
                {
                    components.Add(
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
                            },
                            Update = update
                        }
                    );
                }

                public static void ItemCardStatic(
                    Names.ItemCardName uiName,
                    List<CuiElement> components,
                    string name,
                    ICuiComponent icon,
                    bool hasAmount,
                    int amount
                )
                {
                    components.Add(
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
                        }
                    );

                    components.Add(
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
                        }
                    );
                    components.Add(
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
                        }
                    );
                    components.Add(
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
                    );

                    if (hasAmount)
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
                                        FontSize = 12,
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
                }

                public static void ItemCardButton(
                    Names.ItemCardName uiName,
                    string entryId,
                    InventoryEntryRedeemState redeemState,
                    List<CuiElement> components,
                    bool update
                )
                {
                    string btnColor,
                        btnIconUrl,
                        btnText,
                        btnIconColor,
                        btnTextColor,
                        btnCommand;

                    if (redeemState == InventoryEntryRedeemState.READY)
                    {
                        btnColor = "0.5 0.65 0.5 0.7";
                        btnTextColor = "0.7 0.85 0.7 0.85";
                        btnIconColor = "0.7 0.85 0.7 0.85";
                        btnIconUrl = Icons.REDEEM;
                        btnText = "Redeem";
                        btnCommand = CMD_REDEEM_ITEM + " " + entryId;
                    }
                    else
                    {
                        btnColor = "0.5 0.5 0.5 0.7";
                        btnTextColor = "0.65 0.65 0.65 0.85";
                        btnIconColor = "0.6 0.6 0.6 0.85";
                        btnText = "No\nspace";
                        btnCommand = null;
                        btnIconUrl = Icons.CLOSE_CROSS;

                        switch (redeemState)
                        {
                            case InventoryEntryRedeemState.NO_SPACE:
                                btnText = "No\nspace";
                                btnIconUrl = "https://i.imgur.com/xEwbjZ0.png";
                                break;
                            case InventoryEntryRedeemState.WIPE_BLOCKED:
                                btnText = "Wipe\nblocked";
                                btnIconUrl = Icons.REDEEM_WIPEBLOCKED;
                                break;
                            case InventoryEntryRedeemState.PENDING:
                                btnText = "Redeeming...";
                                btnIconUrl = Icons.NOTIFICATION_PENDING;
                                break;
                            case InventoryEntryRedeemState.FAILED:
                                btnText = "Failed\nclick to retry";
                                btnIconUrl = Icons.REDEEM_RETRY;
                                break;
                        }
                    }

                    components.Add(
                        new CuiElement
                        {
                            Parent = uiName.FooterContainer,
                            Name = uiName.Btn,
                            Components =
                            {
                                new CuiButtonComponent { Command = btnCommand, Color = btnColor },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.5 0",
                                    AnchorMax = "0.99 1"
                                }
                            },
                            Update = update
                        }
                    );
                    components.Add(
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
                            },
                            Update = update
                        }
                    );
                    components.Add(
                        new CuiElement
                        {
                            Parent = uiName.Btn,
                            Name = uiName.BtnText,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = btnText,
                                    FontSize = 12,
                                    Align = TextAnchor.MiddleCenter,
                                    Color = btnTextColor,
                                    Font = Fonts.ROBOTOCONDENSED_REGULAR
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.25 0",
                                    AnchorMax = "1 1"
                                }
                            },
                            Update = update
                        }
                    );
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
                public const string ROBOTOCONDENSED = "robotocondensed.ttf";
            }

            private static class Icons
            {
                public const string NOTIFICATION_ERROR = "https://i.imgur.com/zeCzK3i.png";
                public const string NOTIFICATION_RETRY = "https://i.imgur.com/WUQv83F.png";
                public const string NOTIFICATION_PENDING = "https://i.imgur.com/pCC7jce.png";
                public const string NOTIFICATION_INVENTORY_EMPTY =
                    "https://i.imgur.com/E4WUBOZ.png";
                public const string CLOSE_CROSS = "https://i.imgur.com/zeCzK3i.png";
                public const string ARROW_LEFT = "https://i.imgur.com/TiYyODy.png";
                public const string ARROW_RIGHT = "https://i.imgur.com/tBYlfGM.png";
                public const string ITEM_DEFAULT =
                    "https://api.gmonetize.ru/static/v2/image/plugin/icons/rust_94773.png";
                public const string REDEEM = "https://i.imgur.com/xEwbjZ0.png";
                public const string REDEEM_RETRY = "https://i.imgur.com/WUQv83F.png";
                public const string REDEEM_WIPEBLOCKED = "https://i.imgur.com/pCC7jce.png";
                public const string REDEEM_NOSPACE = "https://i.imgur.com/Od1uXMt.png";
                public const string USER_NOT_FOUND = "https://i.imgur.com/yuGP7Lz.png";
            }

            private enum InventoryEntryRedeemState
            {
                READY,
                NO_SPACE,
                WIPE_BLOCKED,
                PENDING,
                FAILED
            }
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "NotAccessedField.Local")]
        [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
        [SuppressMessage("ReSharper", "UnusedParameter.Local")]
        private static class APIClient
        {
            private static readonly Dictionary<string, string> s_RequestHeaders =
                new Dictionary<string, string>();

            private static string s_InventoryUrl;
            private static string s_RedeemUrl;
            private static string s_IconUrl;
            private static string s_BalanceUrl;
            private static string s_NewPromocodeUrl;
            private static string s_NewInventoryItemUrl;

            public static void Init(string apiUrl, string apiKey)
            {
                s_RequestHeaders["Content-Type"] = "application/json";
                s_RequestHeaders["Authorization"] = $"Bearer {apiKey}";
                s_RequestHeaders["X-USER-PLATFORM"] = "STEAM";

                s_InventoryUrl = apiUrl + "/main/v3/plugin/customer/inventory";
                s_RedeemUrl = apiUrl + "/main/v3/plugin/customer/inventory/$entryid/redeem";
                s_BalanceUrl = apiUrl + "/main/v3/plugin/customer/balance";
                s_NewPromocodeUrl = apiUrl + "/main/v3/plugin/promocode/new";
                s_NewInventoryItemUrl = apiUrl + "/main/v3/plugin/inventory/issue";
                s_IconUrl = apiUrl + "/static/v2/image/$imageid";

                LogDebug(
                    @"
Initialized APIClient with base url {0}, paths are as follows:
Get inventory: {1}
Redeem item: {2}
Get/set balance: {3}
Create promocode: {4}
Add item to user's inventory: {5}
Get icon: {6}
",
                    apiUrl,
                    s_InventoryUrl,
                    s_RedeemUrl,
                    s_BalanceUrl,
                    s_NewPromocodeUrl,
                    s_NewInventoryItemUrl,
                    s_IconUrl
                );
            }

            public static void GetPlayerInventory(
                string userId,
                Action<List<InventoryEntryDto>> okCb,
                Action<int> errorCb
            )
            {
                Instance.webrequest.Enqueue(
                    s_InventoryUrl,
                    null,
                    (code, body) =>
                    {
                        if (code == 200)
                        {
                            okCb(JsonConvert.DeserializeObject<List<InventoryEntryDto>>(body));
                        }
                        else
                        {
                            errorCb(code);
                        }
                    },
                    Instance,
                    RequestMethod.GET,
                    GetRequestHeaders(userId)
                );
            }

            public static void RedeemItem(
                string userId,
                string inventoryEntryId,
                Action okCb,
                Action<int> errorCb
            )
            {
                Instance.webrequest.Enqueue(
                    s_RedeemUrl.Replace("$entryid", inventoryEntryId),
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
                    GetRequestHeaders(userId)
                );
            }

            public static string GetInventoryEntryIconUrl(string iconId)
            {
                return s_IconUrl.Replace("$imageid", iconId);
            }

            public static void GetCustomerBalance(
                string userId,
                Action<long> okCb,
                Action<int> errorCb
            )
            {
                throw new NotImplementedException();
            }

            public static void SetCustomerBalance(string userId, Action okCb, Action<int> errorCb)
            {
                throw new NotImplementedException();
            }

            public static void CreateCustomerInventoryEntry(
                string userId,
                string goodId,
                Action okCb,
                Action<int> errorCb
            )
            {
                throw new NotImplementedException();
            }

            public static void CreatePromocode(
                IReadOnlyDictionary<string, object> promocodeDefinition,
                Action okCb,
                Action<int> errorCb
            )
            {
                throw new NotImplementedException();
            }

            private static Dictionary<string, string> GetRequestHeaders(string userId)
            {
                s_RequestHeaders["X-USER-ID"] = userId;
                return s_RequestHeaders;
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
                public PermissionDto permission;
                public RankDto rank;
                public GoodObjectDto[] contents;

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

            public class GoodObjectDto
            {
                public GoodObjectType type;
                public string name;
                public string iconId;
                public int itemId;
                public int amount;
                public ItemDto.ItemMetaDto meta;
                public string value;
                public string groupName;

                [JsonConverter(typeof(DurationTimeSpanJsonConverter))]
                public TimeSpan? duration;
                public int researchId;

                public enum GoodObjectType
                {
                    ITEM,
                    RESEARCH,
                    COMMAND,
                    PERMISSION,
                    RANK
                }
            }

            public class ItemDto
            {
                public string name;
                public string iconId;
                public int itemId;
                public int amount;
                public ItemMetaDto meta;

                public class ItemMetaDto
                {
                    public ulong? skinId;
                    public float condition;
                }
            }

            public class ResearchDto
            {
                public string name;
                public string iconId;
                public int researchId;
            }

            public class RankDto
            {
                public string name;
                public string iconId;
                public string groupName;

                [JsonConverter(typeof(DurationTimeSpanJsonConverter))]
                public TimeSpan? duration;
            }

            public class PermissionDto
            {
                public string name;
                public string iconId;
                public string value;

                [JsonConverter(typeof(DurationTimeSpanJsonConverter))]
                public TimeSpan? duration;
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

            [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
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

            [JsonConverter(typeof(StringEnumConverter))]
            public enum PermissionGiveMethod
            {
                Auto,
                Internal,
                TimedPermissions,

                // ReSharper disable once InconsistentNaming
                IQPermissions
            }
        }

        private interface IPermissionsIntegrationModule
        {
            /// <summary>
            /// Adds a permission to a player intefinitely
            /// </summary>
            /// <param name="player"></param>
            /// <param name="permissionName"></param>
            void AddPermission(IPlayer player, string permissionName);

            /// <summary>
            /// Adds a permission to a player for certain amount of time
            /// </summary>
            /// <param name="player"></param>
            /// <param name="permissionName"></param>
            /// <param name="duration"></param>
            void AddPermission(IPlayer player, string permissionName, TimeSpan duration);

            /// <summary>
            /// Adds player to a group indefinitely
            /// </summary>
            /// <param name="player"></param>
            /// <param name="groupName"></param>
            void AddGroup(IPlayer player, string groupName);

            /// <summary>
            /// Adds player to a group for certain amount of time
            /// </summary>
            /// <param name="player"></param>
            /// <param name="groupName"></param>
            /// <param name="duration"></param>
            void AddGroup(IPlayer player, string groupName, TimeSpan duration);
        }

        private abstract class PermissionsIntegrationBase : IPermissionsIntegrationModule
        {
            protected readonly gMonetize plugin;

            public PermissionsIntegrationBase(gMonetize plugin)
            {
                plugin = plugin;
            }

            public virtual bool ValidateRequirements()
            {
                return true;
            }

            public void AddPermission(IPlayer player, string permissionName)
            {
                if (!player.HasPermission(permissionName))
                {
                    player.GrantPermission(permissionName);
                }
            }

            public abstract void AddPermission(
                IPlayer player,
                string permissionName,
                TimeSpan duration
            );

            public void AddGroup(IPlayer player, string groupName)
            {
                if (!player.BelongsToGroup(groupName))
                {
                    player.AddToGroup(groupName);
                }
            }

            public abstract void AddGroup(IPlayer player, string groupName, TimeSpan duration);
        }

        private class NativePermissionsIntegration : PermissionsIntegrationBase
        {
            public NativePermissionsIntegration(gMonetize plugin)
                : base(plugin) { }

            public override void AddPermission(
                IPlayer player,
                string permissionName,
                TimeSpan duration
            )
            {
                throw new NotImplementedException();
            }

            public override void AddGroup(IPlayer player, string groupName, TimeSpan duration)
            {
                throw new NotImplementedException();
            }
        }

        private class TimedPermissionsIntegration : PermissionsIntegrationBase
        {
            private StringBuilder _fmtSb;

            public TimedPermissionsIntegration(gMonetize plugin)
                : base(plugin)
            {
                _fmtSb = new StringBuilder();
            }

            public override bool ValidateRequirements()
            {
                if (plugin.plugins.Find("TimedPermissions") == null)
                {
                    plugin.LogWarning(
                        "Permissions integration module warning: TimedPermissions was not found"
                    );
                    return false;
                }

                return true;
            }

            public override void AddPermission(
                IPlayer player,
                string permissionName,
                TimeSpan duration
            )
            {
                plugin.server.Command(
                    "grantperm",
                    player.Id,
                    permissionName,
                    FormatDuration(duration)
                );
            }

            public override void AddGroup(IPlayer player, string groupName, TimeSpan duration)
            {
                plugin.server.Command("addgroup", player.Id, groupName, FormatDuration(duration));
            }

            protected string FormatDuration(TimeSpan timeSpan)
            {
                if (timeSpan.Days > 0)
                {
                    _fmtSb.AppendFormat("{0}d", timeSpan.Days);
                }

                if (timeSpan.Hours > 0)
                {
                    _fmtSb.AppendFormat("{0}h", timeSpan.Hours);
                }

                if (timeSpan.Minutes > 0)
                {
                    _fmtSb.AppendFormat("{0}m", timeSpan.Minutes);
                }

                if (_fmtSb.Length == 0)
                {
                    throw new Exception("Time format is empty");
                }

                string fmt = _fmtSb.ToString();

                return fmt;
            }
        }

        private class IQPermissionsIntegration : TimedPermissionsIntegration
        {
            public IQPermissionsIntegration(gMonetize plugin)
                : base(plugin) { }

            public override bool ValidateRequirements()
            {
                if (plugin.plugins.Find("IQPermissions") == null)
                {
                    plugin.LogWarning(
                        "Permissions integration module warning: IQPermissions was not found"
                    );
                    return false;
                }

                return true;
            }
        }
    }
}
