using System;
using System.Collections.Generic;
using System.Linq;

using CitizenFX.Core;

using MenuAPI;

using static CitizenFX.Core.Native.API;
using static vMenuClient.CommonFunctions;

namespace vMenuClient
{
    /// <summary>
    /// Shared search &amp; filter helpers for the menus that list large numbers of spawnable things
    /// (vehicles, weapons, peds, ...).
    ///
    /// Three mechanisms are offered, because MenuAPI gives us two very different kinds of list:
    ///
    ///  - <see cref="AddCategorySearch"/> adds a search button to a menu that owns category
    ///    submenus, and filters every one of those submenus in place.
    ///  - <see cref="AddFilterHotkey"/> wraps MenuAPI's native FilterMenuItems/ResetFilter on the
    ///    usual Control.Jump hotkey, for a single self-contained list.
    ///  - <see cref="AddListItemSearch"/> covers MenuListItem-backed lists (scenarios, timecycles),
    ///    which are a single horizontal scroll control rather than a list of MenuItems, so neither
    ///    of the above can touch them.
    ///
    /// Everything here is a no-op when the vmenu_enable_menu_search convar is off.
    ///
    /// IMPORTANT: while a MenuAPI filter is active, Size/CurrentIndex/MenuItem.Index and the
    /// itemIndex handed to OnItemSelect all refer to the *filtered* list. Handlers must therefore
    /// dispatch off the MenuItem reference or its ItemData payload, never off a raw index into a
    /// backing data list. Filtering these menus is only safe because that is now true of all of them.
    /// </summary>
    public static class MenuSearch
    {
        /// <summary>
        /// Whether the search/filter features are enabled by the server.
        ///
        /// Read with a default of true rather than via ConfigManager.GetSettingsBool, which defaults
        /// every convar to false. A server whose permissions.cfg predates this feature would
        /// otherwise silently lose every search button with no indication why; opting out has to be
        /// deliberate.
        /// </summary>
        public static bool Enabled => GetConvar("vmenu_enable_menu_search", "true") != "false";

        /// <summary>
        /// net462 has no string.Contains(string, StringComparison) overload.
        /// </summary>
        public static bool ContainsIgnoreCase(string haystack, string needle)
        {
            return !string.IsNullOrEmpty(haystack)
                && !string.IsNullOrEmpty(needle)
                && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// One category submenu that a search filters, plus the button that opens it.
        /// </summary>
        public sealed class Category
        {
            /// <summary>The button on the parent menu that opens <see cref="Menu"/>.</summary>
            public MenuItem Button;

            /// <summary>The submenu holding this category's items.</summary>
            public Menu Menu;

            /// <summary>Display name, used in the no-results message.</summary>
            public string Name;

            // Original button presentation, captured the first time a search touches it so a
            // cleared search can restore exactly what the menu looked like before.
            internal bool Captured;
            internal string OriginalLabel;
            internal string OriginalDescription;
            internal MenuItem.Icon OriginalLeftIcon;
            internal bool OriginalEnabled;
        }

        /// <summary>
        /// Adds a "Search &lt;noun&gt;" button that filters every category submenu in place, rather
        /// than building a separate results list.
        ///
        /// Categories with no matches are marked with a lock icon and a "(0)" label, and opening one
        /// reports the miss and returns to the parent menu instead of showing an empty list.
        /// </summary>
        /// <param name="parent">Menu the search button is added to.</param>
        /// <param name="noun">Plural noun for what is being searched, e.g. "Vehicles".</param>
        /// <param name="categories">
        /// The category submenus to filter. Evaluated once per search so it always reflects the
        /// current menu state.
        /// </param>
        /// <returns>The search button, or null when searching is disabled.</returns>
        public static MenuItem AddCategorySearch(Menu parent, string noun, Func<IEnumerable<Category>> categories)
        {
            if (!Enabled || parent == null || categories == null)
            {
                return null;
            }

            var lowerNoun = noun.ToLower();
            var singular = noun.EndsWith("s") ? lowerNoun.Substring(0, lowerNoun.Length - 1) : lowerNoun;

            // The term currently applied, so a category menu opened later knows why it is empty.
            var activeTerm = "";
            var parentSubtitle = parent.MenuSubtitle;

            var searchButton = new MenuItem($"Search {noun}", "Filter every category by name. Leave the box empty to clear the filter.");
            parent.AddMenuItem(searchButton);

            // Category menus are hooked on first use rather than up front, because this method is
            // called before the categories exist so that the button lands at the top of the menu.
            var hooked = new HashSet<Menu>();

            void EnsureHooked(Category category)
            {
                if (category.Menu == null || !hooked.Add(category.Menu))
                {
                    return;
                }

                var thisCategory = category;

                // Opening a category that the current search emptied reports the miss and goes
                // straight back, rather than presenting an empty list.
                thisCategory.Menu.OnMenuOpen += (m) =>
                {
                    if (string.IsNullOrEmpty(activeTerm) || m.Size > 0)
                    {
                        return;
                    }

                    Notify.Alert($"No {lowerNoun} in ~y~{thisCategory.Name ?? thisCategory.Button?.Text}~s~ match ~y~{activeTerm}~s~.");

                    // Close first, then reopen the parent: opening a menu on top of an already open
                    // one leaves both drawn, which reads as a doubled list.
                    MenuController.CloseAllMenus();
                    parent.OpenMenu();
                };
            }

            void Capture(Category category)
            {
                if (category.Captured)
                {
                    return;
                }

                category.OriginalLabel = category.Button.Label;
                category.OriginalDescription = category.Button.Description;
                category.OriginalLeftIcon = category.Button.LeftIcon;
                category.OriginalEnabled = category.Button.Enabled;
                category.Captured = true;
            }

            void Restore(Category category)
            {
                if (!category.Captured)
                {
                    return;
                }

                category.Button.Label = category.OriginalLabel;
                category.Button.Description = category.OriginalDescription;
                category.Button.LeftIcon = category.OriginalLeftIcon;
                category.Button.Enabled = category.OriginalEnabled;
            }

            void ClearSearch()
            {
                activeTerm = "";

                foreach (var category in categories())
                {
                    if (category?.Menu == null || category.Button == null)
                    {
                        continue;
                    }

                    category.Menu.ResetFilter();
                    category.Menu.RefreshIndex();
                    Restore(category);
                }

                parent.MenuSubtitle = parentSubtitle;
            }

            parent.OnItemSelect += async (_, item, __) =>
            {
                if (item != searchButton)
                {
                    return;
                }

                // GetUserInput returns "" both when the dialog is cancelled and when it is submitted
                // empty, so the two cannot be told apart -- both clear the filter.
                var input = await GetUserInput(windowTitle: $"Search {noun}", defaultText: activeTerm, maxInputLength: 100);

                if (string.IsNullOrWhiteSpace(input))
                {
                    ClearSearch();
                    Subtitle.Custom("Search cleared.");
                    return;
                }

                var term = input.Trim();
                activeTerm = term;

                var matched = 0;

                foreach (var category in categories())
                {
                    if (category?.Menu == null || category.Button == null)
                    {
                        continue;
                    }

                    EnsureHooked(category);
                    Capture(category);

                    // A category the server locked stays locked; searching must not unlock it.
                    if (!category.OriginalEnabled)
                    {
                        continue;
                    }

                    category.Menu.FilterMenuItems(mi => ContainsIgnoreCase(mi.Text, term) || ContainsIgnoreCase(mi.Label, term));
                    category.Menu.RefreshIndex();

                    var count = category.Menu.Size;
                    matched += count;

                    if (count == 0)
                    {
                        category.Button.Label = "(0)";
                        category.Button.LeftIcon = MenuItem.Icon.LOCK;
                        category.Button.Description = $"No {lowerNoun} in this category match \"{term}\".";
                    }
                    else
                    {
                        category.Button.Label = $"({count})";
                        category.Button.LeftIcon = category.OriginalLeftIcon;
                        category.Button.Description = $"{count} {(count == 1 ? singular : lowerNoun)} matching \"{term}\".";
                    }
                }

                parent.MenuSubtitle = $"Search: \"{term}\" - {matched} match{(matched == 1 ? "" : "es")}";

                if (matched == 0)
                {
                    Notify.Alert($"No {lowerNoun} match ~y~{term}~s~.");
                }
                else
                {
                    Subtitle.Custom($"{matched} {(matched == 1 ? singular : lowerNoun)} matching \"{term}\".");
                }
            };

            return searchButton;
        }

        /// <summary>
        /// Wires MenuAPI's native in-place filter onto the Control.Jump hotkey, and clears the
        /// filter when the menu closes. For a single self-contained list whose handlers dispatch off
        /// the MenuItem/ItemData rather than an index.
        /// </summary>
        /// <param name="menu">Menu to make filterable.</param>
        /// <param name="hint">Instructional button text, e.g. "Filter List".</param>
        /// <param name="match">
        /// Optional custom predicate given (item, trimmed term). Defaults to matching the item's
        /// Text and Label.
        /// </param>
        public static void AddFilterHotkey(Menu menu, string hint = "Filter List", Func<MenuItem, string, bool> match = null)
        {
            if (!Enabled || menu == null)
            {
                return;
            }

            async void FilterMenu(Menu m, Control c)
            {
                var input = await GetUserInput(windowTitle: "Filter this list (leave empty to reset the filter)", defaultText: "", maxInputLength: 100);

                if (string.IsNullOrWhiteSpace(input))
                {
                    m.ResetFilter();
                    m.RefreshIndex();
                    Subtitle.Custom("Filter cleared.");
                    return;
                }

                var term = input.Trim();

                if (match != null)
                {
                    m.FilterMenuItems(item => match(item, term));
                }
                else
                {
                    m.FilterMenuItems(item => ContainsIgnoreCase(item.Text, term) || ContainsIgnoreCase(item.Label, term));
                }

                m.RefreshIndex();

                if (m.Size == 0)
                {
                    Notify.Alert($"Nothing in this list matches ~y~{term}~s~.");
                }
                else
                {
                    Subtitle.Custom($"Filter applied: \"{term}\".");
                }
            }

            menu.OnMenuClose += (m) => m.ResetFilter();

            menu.InstructionalButtons.Add(Control.Jump, hint);
            menu.ButtonPressHandlers.Add(new Menu.ButtonPressHandler(Control.Jump, Menu.ControlPressCheckType.JUST_RELEASED, new Action<Menu, Control>(FilterMenu), true));
        }

        /// <summary>
        /// Search for a MenuListItem-backed list. MenuListItem is a single horizontal scroll control
        /// rather than a list of MenuItems, so FilterMenuItems cannot apply -- instead we jump the
        /// selection to the first entry matching the term.
        /// </summary>
        /// <param name="menu">Menu the search button is added to.</param>
        /// <param name="list">The list item whose selection should jump.</param>
        /// <param name="noun">Singular noun, e.g. "Scenario".</param>
        /// <returns>The search button, or null when searching is disabled.</returns>
        public static MenuItem AddListItemSearch(Menu menu, MenuListItem list, string noun)
        {
            if (!Enabled || menu == null || list == null)
            {
                return null;
            }

            var searchButton = new MenuItem($"Search {noun}s", $"Jump the {noun.ToLower()} list to the first entry matching your search.");
            menu.AddMenuItem(searchButton);

            menu.OnItemSelect += async (_, item, __) =>
            {
                if (item != searchButton)
                {
                    return;
                }

                var term = await GetUserInput(windowTitle: $"Search {noun}s", defaultText: "", maxInputLength: 100);
                if (string.IsNullOrWhiteSpace(term))
                {
                    Subtitle.Custom("Search cancelled.");
                    return;
                }

                term = term.Trim();

                var items = list.ListItems;
                for (var i = 0; i < items.Count; i++)
                {
                    if (ContainsIgnoreCase(items[i], term))
                    {
                        list.ListIndex = i;
                        Subtitle.Custom($"Jumped to ~y~{items[i]}~s~.");
                        return;
                    }
                }

                Notify.Alert($"No {noun.ToLower()} found matching ~y~{term}~s~.");
            };

            return searchButton;
        }
    }
}
