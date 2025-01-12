using InteractiveTerminalAPI.UI;
using InteractiveTerminalAPI.UI.Application;
using InteractiveTerminalAPI.UI.Cursor;
using InteractiveTerminalAPI.UI.Screen;
using System;
using System.Linq;
using WeatherProbe.Misc.Util;
using WeatherProbe.Util;

namespace WeatherProbe.Misc.UI.Application
{
    internal class WeatherProbeApplication : PageApplication
    {
        public override void Initialization()
        {
            SelectableLevel[] levels = StartOfRound.Instance.levels.Where(x => x.randomWeathers.Length > 0).ToArray();
            (SelectableLevel[][], CursorMenu[], IScreen[]) entries = GetPageEntries(levels);

            SelectableLevel[][] pagesLevels = entries.Item1;
            CursorMenu[] cursorMenus = entries.Item2;
            IScreen[] screens = entries.Item3;

            for (int i = 0; i < pagesLevels.Length; i++)
            {
                SelectableLevel[] levelList = pagesLevels[i];
                CursorElement[] elements = new CursorElement[levelList.Length];
                cursorMenus[i] = CursorMenu.Create(startingCursorIndex: 0, elements: elements);
                CursorMenu cursorMenu = cursorMenus[i];
                ITextElement[] textElements =
                    [
                        TextElement.Create(text: Constants.MAIN_WEATHER_PROBE_TOP_TEXT),
                        TextElement.Create(text: " "),
                        cursorMenu
                    ];
                screens[i] = BoxedScreen.Create(title: Constants.MAIN_WEATHER_PROBE_SCREEN_TITLE, elements: textElements);

                for (int j = 0; j < levelList.Length; j++)
                {
                    SelectableLevel level = levelList[j];
                    if (level == null) continue;
                    elements[j] = CursorElement.Create(level.PlanetName, action: () => SelectedPlanet(level, PreviousScreen()));
                }
            }
            currentPage = initialPage;
            currentCursorMenu = initialPage.GetCurrentCursorMenu();
            currentScreen = initialPage.GetCurrentScreen();
        }
        void SelectedPlanet(SelectableLevel level, Action cancelAction)
        {
            RandomWeatherWithVariables[] possibleWeathers = level.randomWeathers.Where(x => x.weatherType != level.currentWeather).ToArray();
            CursorElement[] elements = new CursorElement[possibleWeathers.Length+3];

            CursorMenu cursorMenu = new CursorMenu()
            {
                elements = elements,
            };
            IScreen screen = new BoxedScreen()
            {
                Title = level.PlanetName,
                elements =
                [
                    new TextElement()
                        {
                            Text = string.Format(Constants.SELECT_WEATHER_FORMAT, level.PlanetName)
                        },
                        new TextElement()
                        {
                            Text = " "
                        },
                        new TextElement()
                        {
                            Text = string.Format(Constants.CURRENT_WEATHER_FORMAT, level.overrideWeather ? level.overrideWeatherType : (level.currentWeather == LevelWeatherType.None ? "Clear" : level.currentWeather))
                        },
                        new TextElement()
                        {
                            Text = " "
                        },
                        cursorMenu
                ]
            };
            for (int i = 0; i < possibleWeathers.Length; i++)
            {
                RandomWeatherWithVariables weather = possibleWeathers[i];
                elements[i] = new CursorElement()
                {
                    Name = weather.weatherType.ToString(),
                    Action = () =>
                    {
                        BeforeChangeWeather(level, weather.weatherType);
                    },
                    Active = (x) => CanSelectWeather(level, weather.weatherType, Plugin.Config.SPECIFIED_PRICE.Value),
                };

            }
            elements[possibleWeathers.Length] = new CursorElement()
            {
                Name = "Clear",
                Action = () =>
                {
                    BeforeChangeWeather(level, LevelWeatherType.None);
                },
                Active = (x) => CanSelectWeather(level, LevelWeatherType.None, Plugin.Config.RANDOM_ALWAYS_CLEAR ? Plugin.Config.RANDOM_PRICE.Value : Plugin.Config.SPECIFIED_PRICE.Value),
            };
            if (!Plugin.Config.RANDOM_ALWAYS_CLEAR)
            {
                elements[possibleWeathers.Length + 1] = new CursorElement()
                {
                    Name = "Random",
                    Action = () =>
                    {
                        BeforeRandomizeWeather(level);
                    },
                    Active = (x) => CanSelectRandomWeather(possibleWeathers, Plugin.Config.RANDOM_PRICE.Value),
                };

                elements[possibleWeathers.Length + 2] = new CursorElement()
                {
                    Name = Constants.CANCEL_PROMPT,
                    Action = cancelAction
                };
            }
            else
            {
                elements[possibleWeathers.Length + 1] = new CursorElement()
                {
                    Name = Constants.CANCEL_PROMPT,
                    Action = cancelAction
                };
            }
            SwitchScreen(screen, cursorMenu, true);
        }
        static bool CanSelectRandomWeather(RandomWeatherWithVariables[] weathers, int price)
        {
            int groupCredits = Tools.GetTerminal().groupCredits;
            if (price > groupCredits) return false;

            return weathers.Length >= 1;
        }
        static bool CanSelectWeather(SelectableLevel level, LevelWeatherType levelWeatherType, int price)
        {
            int groupCredits = Tools.GetTerminal().groupCredits;
            if (price > groupCredits) return false;

            bool sameWeather = level.overrideWeather ? level.overrideWeatherType == levelWeatherType : level.currentWeather == levelWeatherType;
            return !sameWeather;
        }
        void BeforeChangeWeather(SelectableLevel level, LevelWeatherType type)
        {
            int groupCredits = terminal.groupCredits;
            if (groupCredits < Plugin.Config.SPECIFIED_PRICE.Value)
            {
                ErrorMessage(level.PlanetName, PreviousScreen(), Constants.NOT_ENOUGH_CREDITS_SPECIFIED_PROBE);
                return;
            }

            bool sameWeather = level.overrideWeather ? level.overrideWeatherType == type : level.currentWeather == type;
            if (sameWeather)
            {
                ErrorMessage(level.PlanetName, PreviousScreen(), string.Format(Constants.SAME_WEATHER_FORMAT, level.PlanetName, type == LevelWeatherType.None ? "clear" : type));
                return;
            }
            int price = type == LevelWeatherType.None && Plugin.Config.RANDOM_ALWAYS_CLEAR ? Plugin.Config.RANDOM_PRICE.Value : Plugin.Config.SPECIFIED_PRICE.Value;

            Confirm(level.PlanetName, string.Format(Constants.CONFIRM_WEATHER_FORMAT, level.PlanetName, type, price), () => ChangeWeather(level, type), PreviousScreen());
        }
        void ChangeWeather(SelectableLevel level, LevelWeatherType weatherType)
        {
            int price = weatherType == LevelWeatherType.None && Plugin.Config.RANDOM_ALWAYS_CLEAR ? Plugin.Config.RANDOM_PRICE.Value : Plugin.Config.SPECIFIED_PRICE.Value;
            terminal.groupCredits -= price;
            terminal.SyncGroupCreditsServerRpc(terminal.groupCredits, numItemsInShip: terminal.numberOfItemsInDropship);
            WeatherProbeBehaviour.Instance.SyncWeatherServerRpc(level.PlanetName, weatherType);
            CursorElement exit = new CursorElement()
            {
                Name = "Exit",
                Action = PreviousScreen()
            };
            CursorMenu cursorMenu = new CursorMenu()
            {
                elements = [exit]
            };
            IScreen screen = new BoxedScreen()
            {
                Title = level.PlanetName,
                elements = [
                        new TextElement()
                        {
                            Text = string.Format(Constants.WEATHER_CHANGED_FORMAT, level.PlanetName, weatherType == LevelWeatherType.None ? "clear" : weatherType),
                        },
                        new TextElement()
                        {
                            Text = " "
                        },
                        cursorMenu
                    ]
            };
            SwitchScreen(screen, cursorMenu, false);
        }
        void BeforeRandomizeWeather(SelectableLevel level)
        {
            int groupCredits = Tools.GetTerminal().groupCredits;
            if (groupCredits < Plugin.Config.RANDOM_PRICE.Value)
            {
                ErrorMessage(level.PlanetName, PreviousScreen(), Constants.NOT_ENOUGH_CREDITS_PROBE);
                return;
            }
            Confirm(level.PlanetName, string.Format(Constants.CONFIRM_RANDOM_WEATHER_FORMAT, level.PlanetName, Plugin.Config.RANDOM_PRICE.Value), () => RandomizeWeather(level), PreviousScreen());
        }
        void RandomizeWeather(SelectableLevel level)
        {
            (string, LevelWeatherType weather) weather = WeatherProbeBehaviour.RandomizeWeather(ref level);
            terminal.groupCredits -= Plugin.Config.RANDOM_PRICE.Value;
            terminal.SyncGroupCreditsServerRpc(terminal.groupCredits, numItemsInShip: terminal.numberOfItemsInDropship);
            WeatherProbeBehaviour.Instance.SyncWeatherServerRpc(level.PlanetName, weather.Item2);
            CursorElement exit = new CursorElement()
            {
                Name = "Exit",
                Action = PreviousScreen()
            };
            CursorMenu cursorMenu = new CursorMenu()
            {
                elements = [exit]
            };
            IScreen screen = new BoxedScreen()
            {
                Title = level.PlanetName,
                elements = [
                        new TextElement()
                        {
                            Text = string.Format(Constants.WEATHER_CHANGED_FORMAT, level.PlanetName, weather.Item2),
                        },
                        new TextElement()
                        {
                            Text = " "
                        },
                        cursorMenu
                    ]
            };
            SwitchScreen(screen, cursorMenu, false);
        }
    }
}
