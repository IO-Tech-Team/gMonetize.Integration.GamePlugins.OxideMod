#define DEBUG
// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
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

        [Conditional("DEBUG")]
        private static void LogDebug(string format, params object[] args)
        {
            Interface.uMod.LogDebug("[gMonetize] " + format, args);
        }

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

        private void Init()
        {
            Instance = this;
        }

        private void OnServerInitialized()
        {
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

        private void RegisterCommands()
        {
            covalence.RegisterCommand(CMD_OPEN, this, HandleCommand);
            covalence.RegisterCommand(CMD_CLOSE, this, HandleCommand);
            covalence.RegisterCommand(CMD_NEXT_PAGE, this, HandleCommand);
            covalence.RegisterCommand(CMD_PREV_PAGE, this, HandleCommand);
            covalence.RegisterCommand(CMD_RETRY_LOAD, this, HandleCommand);
            covalence.RegisterCommand(CMD_REDEEM_ITEM, this, HandleCommand);
        }

        private bool HandleCommand(IPlayer player, string command, string[] args)
        {
            switch (command)
            {
                case CMD_OPEN:

                    break;

                case CMD_CLOSE:

                    break;

                case CMD_NEXT_PAGE:

                    break;

                case CMD_PREV_PAGE:

                    break;

                case CMD_RETRY_LOAD:

                    break;

                case CMD_REDEEM_ITEM:

                    break;
            }

            return true;
        }

        private bool TryGetBasePlayer(IPlayer player, out BasePlayer basePlayer)
        {
            return (basePlayer = player.Object as BasePlayer) != null;
        }

        private class UI : FacepunchBehaviour
        {
            private BasePlayer _player;

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

            private void Start()
            {
                LogDebug("UI.Start() on player {0}", _player.IPlayer);

                CuiHelper.AddUi(_player, Builder.Base());
            }

            private void OnDestroy()
            {
                LogDebug("UI.OnDestroy() on player {0}", _player.IPlayer);

                CuiHelper.DestroyUi(_player, Names.MAIN_BACKGROUND);
            }

            private static class Builder
            {
                public static string Base()
                {
                    var components = new List<CuiElement>
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
                                new CuiImageComponent { Color = "0.45 0.45 0.4 0.8" },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.065 0",
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
                                    Color = "0.65 0.65 0.5 0.8"
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
                                new CuiButtonComponent { Color = "0.6 0.2 0.2 0.8", },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.97 0",
                                    AnchorMax = "0.999 1"
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
                                    Url = "https://i.imgur.com/zeCzK3i.png",
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

                    components.AddRange(PaginationButtons(true, true));
                    components.Add(ItemListContainer());
                    components.AddRange(
                        ItemCard(Guid.NewGuid().ToString(), "Some item", 99, "", true)
                    );

                    return CuiHelper.ToJson(components);
                }

                public static IEnumerable<CuiElement> PaginationButtons(bool bNext, bool bPrev)
                {
                    return new[]
                    {
                        new CuiElement
                        {
                            Parent = Names.PAGINATION_BTN_CONTAINER,
                            Name = Names.PAGINATION_BTN_PREV,
                            Components =
                            {
                                new CuiButtonComponent { Color = "0.45 0.45 0.4 0.8" },
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
                                    Url = "https://i.imgur.com/TiYyODy.png",
                                    Color = "0.85 0.85 0.8 0.6"
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
                                new CuiButtonComponent { Color = "0.45 0.45 0.4 0.8" },
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
                                    Url = "https://i.imgur.com/tBYlfGM.png",
                                    Color = "0.85 0.85 0.8 0.6"
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
                    string id,
                    string name,
                    int amount,
                    string iconUrl,
                    bool canRedeem
                )
                {
                    var uiName = Names.ItemCard(id);

                    var components = new List<CuiElement>
                    {
                        new CuiElement
                        {
                            Parent = Names.ITEMLIST_CONTAINER,
                            Name = uiName.Container,
                            Components =
                            {
                                new CuiImageComponent { Color = "0.45 0.45 0.4 0.6" },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0.8",
                                    AnchorMax = "0.1 1"
                                }
                            }
                        }
                    };

                    return components;
                }
            }

            private static class Names
            {
                public const string MAIN_BACKGROUND = "gmonetize/main/background";
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
        }

        private class APIClient
        {
            public class InventoryEntryDto
            {
                public string id;
                public InventoryEntryType type;
                public string name;
                public string iconId;
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
                public TimeSpan? duration;
                public string iconId;
            }
        }

        private class PluginSettings
        {
            public string APIKey { get; set; }
            public string[] ChatCommands { get; set; }
            public PermissionGiveMethod PermissionGiveKind { get; set; }

            public static PluginSettings GetDefaults()
            {
                return new PluginSettings { };
            }

            public enum PermissionGiveMethod
            {
                Internal,
                TimedPermissions
            }
        }
    }
}
