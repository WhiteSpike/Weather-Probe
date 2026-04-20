using InteractiveTerminalAPI.UI;
using InteractiveTerminalAPI.UI.Application;
using InteractiveTerminalAPI.UI.Cursor;
using InteractiveTerminalAPI.UI.Page;
using InteractiveTerminalAPI.UI.Screen;
using System;
using System.Linq;
using WeatherProbe.Misc.Util;
using WeatherProbe.Util;

namespace WeatherProbe.Misc.UI.Application
{
    internal class WeatherProbeApplication : PageApplication<CursorElement>
    {
		protected override int GetEntriesPerPage<T>(T[] entries)
		{
            return 12;
		}
        public override void Initialization()
        {
            if (!WeatherProbeBehaviour.Instance.purchasedModule && Plugin.Config.InitialPurchasePrice.Value > 0)
            {
				CursorElement[] elements =
				[
					CursorElement.Create(name: "Purchase module",active: (node) => CanPurchaseModule(node), action: () => PurchaseModule()),
                    CursorElement.Create(name: "Exit", action: () => UnityEngine.Object.Destroy(InteractiveTerminalManager.Instance))
				];
				CursorMenu<CursorElement> cursorMenu = CursorMenu<CursorElement>.Create(startingCursorIndex: 0, elements: elements);
				IScreen screen = new BoxedScreen()
				{
					Title = Constants.MAIN_WEATHER_PROBE_SCREEN_TITLE,
					elements =
					[
						new TextElement()
						{
							Text = $"Weather Probe module is currently unoperational due to several infractions caused by current crew.\nHowever, with a 'purchase' of {Plugin.Config.InitialPurchasePrice.Value} Company Credits, all infractions will be pardoned and the module will be unlocked.",
						},
						new TextElement()
						{
							Text = " "
						},
						cursorMenu
					]
				};
				currentPage = PageCursorElement<CursorElement>.Create(startingPageIndex: 0, elements: [screen], cursorMenus: [cursorMenu]);
				currentCursorMenu = cursorMenu;
				currentScreen = screen;
				return;
			}
            SelectableLevel[] levels = StartOfRound.Instance.levels.Where(x => x.randomWeathers.Length > 0).ToArray();
            (SelectableLevel[][], BaseCursorMenu<CursorElement>[], IScreen[]) entries = GetPageEntries(levels);

            SelectableLevel[][] pagesLevels = entries.Item1;
            BaseCursorMenu<CursorElement>[] cursorMenus = entries.Item2;
            IScreen[] screens = entries.Item3;

            for (int i = 0; i < pagesLevels.Length; i++)
            {
                SelectableLevel[] levelList = pagesLevels[i];
                CursorElement[] elements = new CursorElement[levelList.Length];
                cursorMenus[i] = CursorMenu<CursorElement>.Create(startingCursorIndex: 0, elements: elements);
                BaseCursorMenu<CursorElement> cursorMenu = cursorMenus[i];
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

		private bool CanPurchaseModule(CursorElement node)
		{
			int groupCredits = Tools.GetTerminal().groupCredits;
            return groupCredits >= Plugin.Config.InitialPurchasePrice;
		}

		void PurchaseModule()
        {
            int groupCredits = Tools.GetTerminal().groupCredits;
			terminal.groupCredits -= Plugin.Config.InitialPurchasePrice;
			terminal.SyncGroupCreditsServerRpc(terminal.groupCredits, numItemsInShip: terminal.numberOfItemsInDropship);
            WeatherProbeBehaviour.Instance.PurchaseModuleServerRpc();
			CursorElement exit = new CursorElement()
			{
				Name = "Exit",
				Action = () => UnityEngine.Object.Destroy(InteractiveTerminalManager.Instance)
			};
			CursorMenu<CursorElement> cursorMenu = new CursorMenu<CursorElement>()
			{
				elements = [exit]
			};
			IScreen screen = new BoxedScreen()
			{
				Title = Constants.MAIN_WEATHER_PROBE_SCREEN_TITLE,
				elements = [
						new TextElement()
						{
							Text = "Your 'purchase' has been successfully transfered.\nYour module is now available for usage, just need to use the command again.\n(Do not speak of this interaction to anyone)",
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
        void SelectedPlanet(SelectableLevel level, Action cancelAction)
        {
            RandomWeatherWithVariables[] possibleWeathers = level.randomWeathers.Where(x => x.weatherType != level.currentWeather && !IsBlacklisted(x.weatherType)).ToArray();
            CursorElement[] elements = new CursorElement[possibleWeathers.Length+3];

            CursorMenu<CursorElement> cursorMenu = new CursorMenu<CursorElement>()
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
                    Active = (x) => CanSelectWeather(level, weather.weatherType, GetWeatherSpecifiedPriceFromConfiguration(weather.weatherType)),
                };

            }
            elements[possibleWeathers.Length] = new CursorElement()
            {
                Name = "Clear",
                Action = () =>
                {
                    BeforeChangeWeather(level, LevelWeatherType.None);
                },
                Active = (x) => CanSelectWeather(level, LevelWeatherType.None, Plugin.Config.RANDOM_ALWAYS_CLEAR ? Plugin.Config.RANDOM_PRICE.Value : GetWeatherSpecifiedPriceFromConfiguration(LevelWeatherType.None)),
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

		bool IsBlacklisted(LevelWeatherType weatherType)
		{
            string weatherName = GetWeatherName(weatherType);
			string[] blacklistedWeathers = Plugin.Config.BlacklistWeathers.Value.Split(PluginConfig.BlacklistWeatherDelimiter);
            for(int i = 0; i < blacklistedWeathers.Length; i++)
            {
                string blacklistedWeather = blacklistedWeathers[i];
                if (weatherName.Equals(blacklistedWeather, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
		}

        static string GetWeatherName(LevelWeatherType weather)
        {
			switch (weather)
			{
				case LevelWeatherType.None: // Clear or no weather
					{
                        return "Clear";
					}
                default: return weather.ToString();
			}
		}


		static int GetWeatherSpecifiedPriceFromConfiguration(LevelWeatherType weather)
		{
            string weatherName = GetWeatherName(weather);
			string[] weatherPrices = Plugin.Config.WeathersPrice.Value.Split(PluginConfig.IndividualWeatherDelimiter);
			for (int i = 0; i < weatherPrices.Length; i++)
			{
				string[] weatherPairing = weatherPrices[i].Trim().Split(PluginConfig.IndividualWeatherPriceDelimiter);
				string weatherPairingName = weatherPairing[0];
				if (!weatherName.Equals(weatherPairingName, StringComparison.OrdinalIgnoreCase)) continue;
				return int.Parse(weatherPairing[1]);
			}
			return Plugin.Config.SPECIFIED_PRICE;
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
			int price = type == LevelWeatherType.None && Plugin.Config.RANDOM_ALWAYS_CLEAR ? Plugin.Config.RANDOM_PRICE.Value : GetWeatherSpecifiedPriceFromConfiguration(type);
			if (groupCredits < price)
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

            Confirm(level.PlanetName, string.Format(Constants.CONFIRM_WEATHER_FORMAT, level.PlanetName, type, price), () => ChangeWeather(level, type), PreviousScreen());
        }
        void ChangeWeather(SelectableLevel level, LevelWeatherType weatherType)
        {
            int price = weatherType == LevelWeatherType.None && Plugin.Config.RANDOM_ALWAYS_CLEAR ? Plugin.Config.RANDOM_PRICE.Value : GetWeatherSpecifiedPriceFromConfiguration(weatherType);
			terminal.groupCredits -= price;
            terminal.SyncGroupCreditsServerRpc(terminal.groupCredits, numItemsInShip: terminal.numberOfItemsInDropship);
            WeatherProbeBehaviour.Instance.SyncWeatherServerRpc(level.PlanetName, weatherType);
            CursorElement exit = new CursorElement()
            {
                Name = "Exit",
                Action = PreviousScreen()
            };
            CursorMenu<CursorElement> cursorMenu = new CursorMenu<CursorElement>()
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
            CursorMenu<CursorElement> cursorMenu = new CursorMenu<CursorElement>()
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
