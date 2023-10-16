#define DEBUG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
// ReSharper disable CompareOfFloatsByEqualityOperator

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

namespace Oxide.Plugins
{
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once ClassNeverInstantiated.Global
    [Info("gMonetize", "gMonetize Project", "2.0.0")]
    [Description("gMonetize integration with OxideMod")]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
    public class gMonetize : CovalencePlugin
    {
        private const string PERMISSION_USE = "gmonetize.use";
        private const string CMD_OPEN = "gmonetize.open";
        private const string CMD_CLOSE = "gmonetize.close";
        private const string CMD_NEXT_PAGE = "gmonetize.nextpage";
        private const string CMD_PREV_PAGE = "gmonetize.prevpage";
        private const string CMD_RETRY_LOAD = "gmonetize.retryload";
        private const string CMD_REDEEM_ITEM = "gmonetize.redeemitem";

        private static readonly string[] s_UserIdReplacers =
        {
            "${userid}",
            "%STEAMID%",
            "%userid%",
            "${steamid}",
            "$userid",
            "$steamid",
            "$player.id",
            "$user.id"
        };
        private static readonly string[] s_UserNameReplacers =
        {
            "${username}",
            "%USERNAME%",
            "${displayname}",
            "%displayname%",
            "$username",
            "$displayname",
            "$player.name",
            "$user.name"
        };

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
                    string inventoryEntryId = args[0];

                    LogDebug("Player {0} wants to receive item {1}", player, inventoryEntryId);

                    APIClient.RedeemItem(
                        player.Id,
                        inventoryEntryId,
                        () =>
                        {
                            LogDebug(
                                "API has responded with SUCCESS to an attempt of the player {0} to redeem item {1}",
                                player,
                                inventoryEntryId
                            );

                            UI component = basePlayer.GetComponent<UI>();
                            APIClient.InventoryEntryDto inventoryEntry =
                                component.GetCachedInventoryEntry(inventoryEntryId);

                            UI.InventoryEntryRedeemState redeemState = GetInventoryEntryRedeemState(
                                basePlayer,
                                inventoryEntry
                            );

                            if (!CanRedeemItemWithState(redeemState))
                            {
                                component.gMonetize_RedeemItemGiveError(
                                    inventoryEntry.id,
                                    redeemState
                                );
                            }
                            else
                            {
                                IssueInventoryEntry(basePlayer, inventoryEntry);
                                component.gMonetize_RedeemItemOk(inventoryEntry.id);
                            }
                        },
                        code =>
                        {
                            LogDebug(
                                "API has responded with FAIL (code {0}) to an attempt of the player {1} to redeem item {2}",
                                code,
                                player,
                                inventoryEntryId
                            );

                            basePlayer.SendMessage(
                                nameof(UI.gMonetize_RedeemItemRequestError),
                                new object[] { inventoryEntryId, code }
                            );
                        }
                    );
                    basePlayer.SendMessage(
                        nameof(UI.gMonetize_RedeemItemPending),
                        inventoryEntryId
                    );
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

        #region InventoryEntry issuing helpers

        private void IssueInventoryEntry(BasePlayer player, APIClient.InventoryEntryDto entry)
        {
            switch (entry.type)
            {
                case APIClient.InventoryEntryDto.InventoryEntryType.ITEM:
                    IssueItemInventoryEntry(player, entry.item);
                    break;
                case APIClient.InventoryEntryDto.InventoryEntryType.RESEARCH:
                    IssueResearchInventoryEntry(player, entry.research);
                    break;
                case APIClient.InventoryEntryDto.InventoryEntryType.KIT:
                    IssueKitInventoryEntry(player, entry.contents);
                    break;
                case APIClient.InventoryEntryDto.InventoryEntryType.PERMISSION:
                    IssuePermissionInventoryEntry(player, entry.permission);
                    break;
                case APIClient.InventoryEntryDto.InventoryEntryType.RANK:
                    IssueGroupInventoryEntry(player, entry.rank);
                    break;
                case APIClient.InventoryEntryDto.InventoryEntryType.CUSTOM:
                    IssueCustomInventoryEntry(player, entry.contents);
                    break;
            }
        }

        private void IssueCustomInventoryEntry(
            BasePlayer player,
            IEnumerable<APIClient.GoodObjectDto> contents
        )
        {
            foreach (APIClient.GoodObjectDto good in contents)
            {
                switch (good.type)
                {
                    case APIClient.GoodObjectDto.GoodObjectType.ITEM:
                        Item item = CreateItemFromDto(good);
                        player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
                        break;
                    case APIClient.GoodObjectDto.GoodObjectType.RESEARCH:
                        ItemDefinition itemDef = good.GetItemDefinition();
                        player.blueprints.Unlock(itemDef);
                        break;
                    case APIClient.GoodObjectDto.GoodObjectType.COMMAND:

                        break;
                    case APIClient.GoodObjectDto.GoodObjectType.PERMISSION:
                        IssuePermissionInventoryEntry(player, good);
                        break;
                    case APIClient.GoodObjectDto.GoodObjectType.RANK:
                        IssueGroupInventoryEntry(player, good);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private void IssueCommandInventoryEntry(BasePlayer player, APIClient.GoodObjectDto dto)
        {
            string format = dto.value;

            string command = ReplaceUserPlaceholders(player, format);

            LogDebug(
                "Executing command on player {0}: {1} => {2}",
                player.IPlayer,
                format,
                command
            );

            server.Command(command);
        }

        private string ReplaceUserPlaceholders(BasePlayer player, string format)
        {
            string msg = format;

            foreach (string replacer in s_UserIdReplacers)
            {
                msg = msg.Replace(
                    replacer,
                    player.UserIDString,
                    StringComparison.OrdinalIgnoreCase
                );
            }

            foreach (string replacer in s_UserNameReplacers)
            {
                msg = msg.Replace(replacer, player.displayName);
            }

            return msg;
        }

        private void IssuePermissionInventoryEntry(BasePlayer player, APIClient.PermissionDto dto)
        {
            _permissionsIntegrationModule.AddPermission(player.IPlayer, dto.value, dto.duration);
        }

        private void IssuePermissionInventoryEntry(BasePlayer player, APIClient.GoodObjectDto dto)
        {
            _permissionsIntegrationModule.AddPermission(player.IPlayer, dto.value, dto.duration);
        }

        private void IssueGroupInventoryEntry(BasePlayer player, APIClient.RankDto dto)
        {
            _permissionsIntegrationModule.AddGroup(player.IPlayer, dto.groupName, dto.duration);
        }

        private void IssueGroupInventoryEntry(BasePlayer player, APIClient.GoodObjectDto dto)
        {
            _permissionsIntegrationModule.AddGroup(player.IPlayer, dto.groupName, dto.duration);
        }

        private void IssueKitInventoryEntry(
            BasePlayer player,
            IEnumerable<APIClient.GoodObjectDto> contents
        )
        {
            foreach (APIClient.GoodObjectDto good in contents)
            {
                if (good.type == APIClient.GoodObjectDto.GoodObjectType.ITEM)
                {
                    Item item = CreateItemFromDto(good);
                    player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
                }
                else
                {
                    ItemDefinition itemDef = good.GetItemDefinition();
                    player.blueprints.Unlock(itemDef);
                }
            }
        }

        private Item CreateItemFromDto(APIClient.ItemDto dto)
        {
            ItemDefinition def = dto.GetItemDefinition();

            Item item = ItemManager.Create(def, dto.amount, dto.meta.skinId ?? 0UL);
            if (dto.meta.condition != 1.0f)
            {
                item.conditionNormalized = dto.meta.condition;
            }

            return item;
        }

        private Item CreateItemFromDto(APIClient.GoodObjectDto dto)
        {
            ItemDefinition def = dto.GetItemDefinition();

            Item item = ItemManager.Create(def, dto.amount, dto.meta.skinId ?? 0UL);
            if (dto.meta.condition != 1.0f)
            {
                item.conditionNormalized = dto.meta.condition;
            }

            return item;
        }

        private void IssueResearchInventoryEntry(BasePlayer player, APIClient.ResearchDto dto)
        {
            ItemDefinition itemDef = dto.GetItemDefinition();
            player.blueprints.Unlock(itemDef);
        }

        private void IssueItemInventoryEntry(BasePlayer player, APIClient.ItemDto dto)
        {
            Item item = CreateItemFromDto(dto);

            player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
        }

        #endregion

        #region Issuing availability helpers

        private bool CanRedeemItemWithState(UI.InventoryEntryRedeemState state)
        {
            return state == UI.InventoryEntryRedeemState.READY
                || state == UI.InventoryEntryRedeemState.FAILED;
        }

        private UI.InventoryEntryRedeemState GetInventoryEntryRedeemState(
            BasePlayer player,
            APIClient.InventoryEntryDto entry
        )
        {
            UI.InventoryEntryRedeemState state;

            if (IsWipeBlocked(entry))
            {
                state = UI.InventoryEntryRedeemState.WIPE_BLOCKED;
            }
            else
            {
                switch (entry.type)
                {
                    case APIClient.InventoryEntryDto.InventoryEntryType.ITEM:
                        state = GetItemEntryRedeemState(player, entry);
                        break;

                    case APIClient.InventoryEntryDto.InventoryEntryType.KIT:
                        state = GetKitEntryRedeemState(player, entry);
                        break;

                    case APIClient.InventoryEntryDto.InventoryEntryType.RESEARCH:
                        state = GetResearchEntryRedeemState(player, entry);
                        break;

                    case APIClient.InventoryEntryDto.InventoryEntryType.CUSTOM:
                        state = GetCustomEntryRedeemState(player, entry);
                        break;

                    default:
                        state = UI.InventoryEntryRedeemState.READY;
                        break;
                }
            }

            return state;
        }

        private UI.InventoryEntryRedeemState GetCustomEntryRedeemState(
            BasePlayer player,
            APIClient.InventoryEntryDto entry
        )
        {
            foreach (APIClient.GoodObjectDto good in entry.contents)
            {
                if (good.type == APIClient.GoodObjectDto.GoodObjectType.ITEM)
                {
                    ItemDefinition itemDef = good.GetItemDefinition();

                    if (good.amount > GetMaxAmountOfItemAvailableToReceive(player, itemDef))
                    {
                        return UI.InventoryEntryRedeemState.NO_SPACE;
                    }
                }
                else if (good.type == APIClient.GoodObjectDto.GoodObjectType.RESEARCH)
                {
                    ItemDefinition itemDef = good.GetItemDefinition();

                    if (player.blueprints.IsUnlocked(itemDef))
                    {
                        return UI.InventoryEntryRedeemState.RESEARCHED;
                    }
                }
            }

            return UI.InventoryEntryRedeemState.READY;
        }

        private UI.InventoryEntryRedeemState GetKitEntryRedeemState(
            BasePlayer player,
            APIClient.InventoryEntryDto entry
        )
        {
            foreach (APIClient.GoodObjectDto good in entry.contents)
            {
                ItemDefinition itemDef = good.GetItemDefinition();

                if (good.type == APIClient.GoodObjectDto.GoodObjectType.ITEM)
                {
                    if (good.amount > GetMaxAmountOfItemAvailableToReceive(player, itemDef))
                    {
                        return UI.InventoryEntryRedeemState.NO_SPACE;
                    }
                }
                else if (player.blueprints.IsUnlocked(itemDef))
                {
                    return UI.InventoryEntryRedeemState.RESEARCHED;
                }
            }

            return UI.InventoryEntryRedeemState.READY;
        }

        private UI.InventoryEntryRedeemState GetResearchEntryRedeemState(
            BasePlayer player,
            APIClient.InventoryEntryDto research
        )
        {
            ItemDefinition itemDef = research.research.GetItemDefinition();

            return player.blueprints.IsUnlocked(itemDef)
                ? UI.InventoryEntryRedeemState.RESEARCHED
                : UI.InventoryEntryRedeemState.READY;
        }

        private UI.InventoryEntryRedeemState GetItemEntryRedeemState(
            BasePlayer player,
            APIClient.InventoryEntryDto item
        )
        {
            UI.InventoryEntryRedeemState state;

            ItemDefinition itemDef = item.item.GetItemDefinition();

            int maxAmount = GetMaxAmountOfItemAvailableToReceive(player, itemDef);

            if (item.item.amount <= maxAmount)
            {
                state = UI.InventoryEntryRedeemState.READY;
            }
            else
            {
                state = UI.InventoryEntryRedeemState.NO_SPACE;
            }

            return state;
        }

        private int GetInventoryFreeSlots(BasePlayer player)
        {
            ItemContainer cb = player.inventory.containerBelt;
            ItemContainer cm = player.inventory.containerMain;

            return (cb.capacity - cb.itemList.Count) + (cm.capacity - cm.itemList.Count);
        }

        private int GetAmountOfItemAvailableForStacking(
            BasePlayer player,
            ItemDefinition itemDefinition
        )
        {
            int maxStack = itemDefinition.stackable;
            List<Item> itemList = Pool.GetList<Item>();
            itemList.AddRange(player.inventory.containerBelt.itemList);
            itemList.AddRange(player.inventory.containerMain.itemList);

            int availableAmount = 0;
            foreach (Item item in itemList)
            {
                if (item.info != itemDefinition)
                {
                    continue;
                }

                availableAmount += maxStack - item.amount;
            }

            Pool.FreeList(ref itemList);

            return availableAmount;
        }

        private int GetMaxAmountOfItemAvailableToReceive(
            BasePlayer player,
            ItemDefinition itemDefinition
        )
        {
            int maxAvailableToStack = GetAmountOfItemAvailableForStacking(player, itemDefinition);
            int freeSlots = GetInventoryFreeSlots(player);
            int maxStack = itemDefinition.stackable;

            return maxAvailableToStack + maxStack * freeSlots;
        }

        private bool IsWipeBlocked(APIClient.InventoryEntryDto inventoryEntry)
        {
            if (inventoryEntry.wipeBlockDuration == null)
            {
                return false;
            }

            TimeSpan timeSinceWipe = DateTime.Now - SaveRestore.SaveCreatedTime;

            return inventoryEntry.wipeBlockDuration < timeSinceWipe;
        }

        #endregion

        #region Generic helpers

        private bool TryGetBasePlayer(IPlayer player, out BasePlayer basePlayer)
        {
            return (basePlayer = player.Object as BasePlayer) != null;
        }

        #endregion

        private class UI : FacepunchBehaviour
        {
            private const int ITEMS_PER_PAGE = Builder.CARD_COUNT;
            private BasePlayer _player;
            private bool _isOpen,
                _isItemListContainerDrawn;
            private NotificationState _notificationState;
            private List<APIClient.InventoryEntryDto> _inventory;
            private List<InventoryCard> _cards;
            private Dictionary<string, APIClient.InventoryEntryDto> _idToDto;
            private Dictionary<string, InventoryCard> _idToCard;
            private int _currentPageId;

            #region Unity events

            private void Awake()
            {
                _player = GetComponent<BasePlayer>();
                _cards = new List<InventoryCard>();
                _idToDto = new Dictionary<string, APIClient.InventoryEntryDto>();
                _idToCard = new Dictionary<string, InventoryCard>();
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
                if (_inventory == null || _inventory.Count == 0)
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

            private int CountItemsOnCurrentPage()
            {
                return CountItemsOnPage(_currentPageId);
            }

            private int CountItemsOnPage(int pageIndex)
            {
                return _inventory.Count - ITEMS_PER_PAGE * pageIndex;
            }

            #endregion

            #region Message handlers

            public void gMonetize_OpenUI()
            {
                if (_isOpen)
                {
                    return;
                }

                CuiHelper.AddUi(_player, Builder.Base());
                DrawPaginationButtons(true);
                _isOpen = true;
            }

            public void gMonetize_LoadingItems()
            {
                DrawNotification(NotificationState.LoadingItems);
            }

            public void gMonetize_CloseUI()
            {
                CloseAndReleaseInventory();
            }

            public void gMonetize_RedeemItemOk(string inventoryEntryId)
            {
                APIClient.InventoryEntryDto inventoryEntry;

                if (!_idToDto.TryGetValue(inventoryEntryId, out inventoryEntry))
                {
                    Instance.LogWarning(
                        "gMonetize_RedeemItemOk: Failed to find inventory entry {0} (player {1})",
                        inventoryEntryId,
                        _player.UserIDString
                    );
                    return;
                }

                _inventory.Remove(inventoryEntry);
                
                if (!_inventory.Any())
                {
                    RemoveItemListContainer();
                    DrawNotification(NotificationState.InventoryEmpty);
                    return;
                }

                if (_cards.Count == 1)
                {
                    RemoveAllCards(true);
                    _currentPageId--;
                    DrawInventoryPage();
                }
                else
                {
                    RemoveInventoryCard(inventoryEntryId);
                }
            }

            private void RemoveInventoryCard(string inventoryEntryId)
            {
                InventoryCard card;
                if (!_idToCard.TryGetValue(inventoryEntryId, out card))
                {
                    LogDebug("RemoveInventoryCard({0}): failed to find card");
                    return;
                }

                CuiHelper.DestroyUi(_player, card.UIName().Container);
                _idToCard.Remove(inventoryEntryId);
                _cards.RemoveAt(card.ContainerIndex);

                if (card.ContainerIndex != _cards.Count)
                {
                    List<CuiElement> components = Pool.GetList<CuiElement>();
                    for (int i = card.ContainerIndex; i < _cards.Count; i++)
                    {
                        InventoryCard card2 = _cards[i];
                        card2.ChangeIndex(i, components);
                    }

                    CuiHelper.AddUi(_player, components);

                    Pool.FreeList(ref components);
                }

                InventoryCard lastCard = _cards.Last();
                string lastCardEntryId = lastCard.EntryId;
                APIClient.InventoryEntryDto lastCardDto = _idToDto[lastCardEntryId];
                int lastCardDtoIndex = _inventory.IndexOf(lastCardDto);

                if (lastCardDtoIndex == _inventory.Count-1)
                {
                    return;
                }

                APIClient.InventoryEntryDto nextCardDto = _inventory[lastCardDtoIndex + 1];

                InventoryCard nextCard = new InventoryCard(_cards.Count, nextCardDto, Instance.GetInventoryEntryRedeemState(_player, nextCardDto));
                _cards.Add(nextCard);
                _idToCard.Add(nextCard.EntryId, nextCard);
                List<CuiElement> cList = Pool.GetList<CuiElement>();
                nextCard.Build(cList);
                CuiHelper.AddUi(_player, cList);
                Pool.FreeList(ref cList);
            }

            public void gMonetize_RedeemItemGiveError(string id, InventoryEntryRedeemState state)
            {
                InventoryCard card = _cards.Find(x => x.EntryId == id);

                if (card == null)
                {
                    throw new Exception("Failed to find rendered card " + id);
                }

                List<CuiElement> components = Pool.GetList<CuiElement>();
                if (card.ChangeRedeemState(InventoryEntryRedeemState.FAILED, components))
                {
                    CuiHelper.AddUi(_player, components);
                }
                else
                {
                    LogDebug("Called _RedeemItemGiveError, but state hasn't changed {0}", id);
                }

                Pool.FreeList(ref components);
            }

            public void gMonetize_RedeemItemRequestError(object[] args)
            {
                string inventoryEntryId = (string)args[0];
                int errorCode = (int)args[1];

                InventoryCard card = _idToCard[inventoryEntryId];

                if (card == null)
                {
                    LogDebug(
                        "gMonetize_RedeemItemRequestError: Failed to find rendered card {0} in ui of player {1}",
                        inventoryEntryId,
                        _player.UserIDString
                    );
                    return;
                }

                if (errorCode == 404)
                {
                    RemoveInventoryCard(inventoryEntryId);
                }
                else
                {
                    List<CuiElement> components = Pool.GetList<CuiElement>();
                    card.ChangeRedeemState(InventoryEntryRedeemState.FAILED, components);
                    CuiHelper.AddUi(_player, components);
                    Pool.FreeList(ref components);
                }
            }

            public void gMonetize_InventoryReceived(
                List<APIClient.InventoryEntryDto> inventoryEntries
            )
            {
                LogDebug("gMonetize_InventoryReceived(Count:{0})", inventoryEntries.Count);
                if (!_isOpen)
                {
                    LogDebug("UI is not open");
                    return;
                }

                if (!inventoryEntries.Any())
                {
                    DrawNotification(NotificationState.InventoryEmpty);
                    return;
                }

                _inventory = inventoryEntries;
                _inventory.ForEach(x => _idToDto.Add(x.id, x));

                RemoveNotification();
                DrawInventoryPage();
                DrawPaginationButtons();
            }

            public void gMonetize_InventoryReceiveFail(int errorCode)
            {
                LogDebug("gMonetize_InventoryReceiveFail({0})", errorCode);
                if (!_isOpen)
                {
                    LogDebug("UI is not open");
                    return;
                }

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
                    LogDebug("gMonetize_PreviousPage(): No previous page");
                    return;
                }

                RemoveAllCards(true);
                _currentPageId--;
                DrawInventoryPage();
                DrawPaginationButtons();
            }

            public void gMonetize_NextPage()
            {
                if (!HasNextPage() || !_isOpen)
                {
                    LogDebug("gMonetize_NextPage(): No next page");
                    return;
                }

                RemoveAllCards(true);
                _currentPageId++;
                DrawInventoryPage();
                DrawPaginationButtons();
            }

            public void gMonetize_RedeemItemPending(string id)
            {
                LogDebug("gMonetize_RedeemItemPending({0})", id);
                if (!_isOpen)
                {
                    LogDebug("UI is not open");
                    return;
                }

                InventoryCard card;

                if (!_idToCard.TryGetValue(id, out card))
                {
                    LogDebug("Failed to find rendered item card");
                    return;
                }

                List<CuiElement> components = Pool.GetList<CuiElement>();
                if (card.ChangeRedeemState(InventoryEntryRedeemState.PENDING, components))
                {
                    CuiHelper.AddUi(_player, components);
                }

                Pool.FreeList(ref components);
            }

            #endregion

            public APIClient.InventoryEntryDto GetCachedInventoryEntry(string id)
            {
                return _idToDto[id];
            }

            private void CloseAndReleaseInventory()
            {
                if (!_isOpen)
                {
                    return;
                }

                CuiHelper.DestroyUi(_player, Names.MAIN_BACKGROUND);
                RemoveAllCards(false);
                _isItemListContainerDrawn = false;
                _notificationState = NotificationState.None;
                _inventory = null;
                _idToDto.Clear();
                _currentPageId = 0;
                _isOpen = false;
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

                RemoveAllCards(false);
                CuiHelper.DestroyUi(_player, Names.ITEMLIST_CONTAINER);
                _isItemListContainerDrawn = false;
            }

            private void RemoveAllCards(bool destroy)
            {
                if (destroy)
                {
                    _cards.ForEach(c => CuiHelper.DestroyUi(_player, c.UIName().Container));
                }

                _cards.Clear();
                _idToCard.Clear();
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

            private void DrawInventoryPage()
            {
                DrawItemListContainer();

                List<APIClient.InventoryEntryDto> itemList = CurrentPageItems();
                List<CuiElement> componentList = new List<CuiElement>();
                for (int i = 0; i < itemList.Count; i++)
                {
                    APIClient.InventoryEntryDto entry = itemList[i];

                    InventoryCard card = new InventoryCard(
                        i,
                        entry,
                        Instance.GetInventoryEntryRedeemState(_player, entry)
                    );
                    card.Build(componentList);
                    _cards.Add(card);
                    _idToCard.Add(entry.id, card);
                }

                LogDebug(
                    "DrawInventoryPage. Itemcount: {0}, Component count: {1}",
                    itemList.Count,
                    componentList.Count
                );

                CuiHelper.AddUi(_player, componentList);
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
                else
                {
                    RemoveItemListContainer();
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

                public Names.ItemCardName UIName() => Names.ItemCard(EntryId);

                public void Build(List<CuiElement> components)
                {
                    Names.ItemCardName uiName = Names.ItemCard(EntryId);
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
                                    Command = bPrev ? CMD_PREV_PAGE : null
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
                                    Command = bNext ? CMD_NEXT_PAGE : null
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

            public enum InventoryEntryRedeemState
            {
                READY,
                NO_SPACE,
                RESEARCHED,
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
                            try
                            {
                                LogDebug("Deserializing inventory: {0}", body);
                                okCb(JsonConvert.DeserializeObject<List<InventoryEntryDto>>(body));
                            }
                            catch
                            {
                                errorCb(500);
                            }
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

                public ItemDefinition GetItemDefinition()
                {
                    if (type == GoodObjectType.ITEM)
                        return ItemManager.FindItemDefinition(itemId);
                    if (type == GoodObjectType.RESEARCH)
                        return ItemManager.FindItemDefinition(researchId);
                    throw new InvalidOperationException(
                        "Cannot GetItemDefinition() on " + type.ToString("G") + " GoodObjectDto"
                    );
                }

                public Item CreateItem()
                {
                    return ItemManager.Create(GetItemDefinition(), amount, meta.skinId ?? 0);
                }

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

                public Item CreateItem()
                {
                    return ItemManager.Create(GetItemDefinition(), amount, meta.skinId ?? 0);
                }

                public ItemDefinition GetItemDefinition()
                {
                    ItemDefinition itemDef = ItemManager.FindItemDefinition(itemId);
                    if (!itemDef)
                    {
                        throw new Exception(
                            "Failed to find ItemDefinition in the itemDTO: " + name + " " + itemId
                        );
                    }

                    return itemDef;
                }

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

                public ItemDefinition GetItemDefinition()
                {
                    ItemDefinition itemDef = ItemManager.FindItemDefinition(researchId);
                    if (!itemDef)
                    {
                        throw new Exception(
                            "Failed to find ItemDefinition in the researchDTO: "
                                + name
                                + " "
                                + researchId
                        );
                    }

                    return itemDef;
                }
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
                                seconds = (int)float.Parse(partBuf, CultureInfo.InvariantCulture);
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
            void AddPermission(IPlayer player, string permissionName, TimeSpan? duration);

            void AddGroup(IPlayer player, string groupName, TimeSpan? duration);
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

            public virtual void AddPermission(
                IPlayer player,
                string permissionName,
                TimeSpan? duration
            )
            {
                if (duration.HasValue)
                {
                    throw new NotSupportedException();
                }

                if (!player.HasPermission(permissionName))
                {
                    player.GrantPermission(permissionName);
                }
            }

            public virtual void AddGroup(IPlayer player, string groupName, TimeSpan? duration)
            {
                if (duration.HasValue)
                {
                    throw new NotSupportedException();
                }

                if (!player.BelongsToGroup(groupName))
                {
                    player.AddToGroup(groupName);
                }
            }
        }

        private class NativePermissionsIntegration : PermissionsIntegrationBase
        {
            public NativePermissionsIntegration(gMonetize plugin)
                : base(plugin) { }

            public override void AddPermission(
                IPlayer player,
                string permissionName,
                TimeSpan? duration
            )
            {
                throw new NotImplementedException();
            }

            public override void AddGroup(IPlayer player, string groupName, TimeSpan? duration)
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
                TimeSpan? duration
            )
            {
                plugin.server.Command(
                    "grantperm",
                    player.Id,
                    permissionName,
                    FormatDuration(duration.Value)
                );
            }

            public override void AddGroup(IPlayer player, string groupName, TimeSpan? duration)
            {
                plugin.server.Command(
                    "addgroup",
                    player.Id,
                    groupName,
                    FormatDuration(duration.Value)
                );
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
