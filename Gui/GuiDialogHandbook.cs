﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BrowseHistoryElement
    {
        public GuiHandbookPage Page;
        public string SearchText;
        public float PosY;
    }

    // Concept:
    // 1. Pressing H opens the GuiDialogKnowledgeBase
    // 2. Top of the dialog has a search field to search for blocks and items
    //    While hovering an itemstack in an itemslot it will pre-search the info of that item
    // The block/item detail page contains
    // - An icon of the block item
    // - Name and description
    // - Where it can be found (Dropped by: Block x, Monster y)
    // - In which recipes in can be used (Grip recipe X, Smithing recipe z)

    // By default every item and block in the creative inventory can be found through search
    // but can be explicitly made to be included or excluded using item/block attributes
    public class GuiDialogHandbook : GuiDialog
    {
        Dictionary<string, int> pageNumberByPageCode = new Dictionary<string, int>();
        
        List<GuiHandbookPage> allHandbookPages = new List<GuiHandbookPage>();
        List<GuiHandbookPage> shownHandbookPages = new List<GuiHandbookPage>();

        ItemStack[] allstacks;
        List<string> categoryCodes = new List<string>();

        Stack<BrowseHistoryElement> browseHistory = new Stack<BrowseHistoryElement>();
        
        string currentSearchText;
        public string currentCatgoryCode;


        GuiComposer overviewGui;
        GuiComposer detailViewGui;

        double listHeight = 500;

        

        public override string ToggleKeyCombinationCode => "handbook";

        public GuiDialogHandbook(ICoreClientAPI capi) : base(capi)
        {
            IPlayerInventoryManager invm = capi.World.Player.InventoryManager;

            capi.Settings.AddWatcher<float>("guiScale", (float val) => {
                initOverviewGui();
                foreach (GuiHandbookPage elem in shownHandbookPages)
                {
                    elem.Dispose();
                }
            });

            capi.RegisterCommand("reloadhandbook", "Reload handbook entries", "", cReload);
            loadEntries();
        }

        void loadEntries()
        {
            pageNumberByPageCode.Clear();
            shownHandbookPages.Clear();
            allHandbookPages.Clear();

            InitStackCacheAndStacks();
            HashSet<string> codes = initCustomPages();
            codes.Add("stack");
            this.categoryCodes = codes.ToList();
            initOverviewGui();
        }

        private void cReload(int groupId, CmdArgs args)
        {
            capi.Assets.Reload(AssetCategory.config);
            Lang.Load(capi.World.Logger, capi.Assets, capi.Settings.String["language"]);
            loadEntries();
            capi.ShowChatMessage("Lang file and handbook entries now reloaded");
        }

        public void initOverviewGui()
        {
            ElementBounds searchFieldBounds = ElementBounds.Fixed(GuiStyle.ElementToDialogPadding - 2, 45, 300, 30);
            ElementBounds stackListBounds = ElementBounds.Fixed(0, 0, 500, listHeight).FixedUnder(searchFieldBounds, 5);

            ElementBounds clipBounds = stackListBounds.ForkBoundingParent();
            ElementBounds insetBounds = stackListBounds.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);

            ElementBounds scrollbarBounds = insetBounds.CopyOffsetedSibling(3 + stackListBounds.fixedWidth + 7).WithFixedWidth(20);

            ElementBounds closeButtonBounds = ElementBounds
                .FixedSize(0, 0)
                .FixedUnder(clipBounds, 2 * 5 + 8)
                .WithAlignment(EnumDialogArea.RightFixed)
                .WithFixedPadding(20, 4)
                .WithFixedAlignmentOffset(2, 0)
            ;

            // 2. Around all that is 10 pixel padding
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(insetBounds, stackListBounds, scrollbarBounds, closeButtonBounds);

            // 3. Finally Dialog
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.None).WithAlignment(EnumDialogArea.CenterFixed).WithFixedPosition(0, 70);

            ElementBounds tabBounds = ElementBounds.Fixed(-200, 35, 200, 545);

            int curTab;
            ElementBounds backButtonBounds = ElementBounds
                .FixedSize(0, 0)
                .FixedUnder(clipBounds, 2 * 5 + 5)
                .WithAlignment(EnumDialogArea.LeftFixed)
                .WithFixedPadding(20, 4)
                .WithFixedAlignmentOffset(-6, 3)
            ;

            overviewGui = capi.Gui
                .CreateCompo("handbook-overview", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(Lang.Get("Survival Handbook"), OnTitleBarClose)
                .AddVerticalTabs(genTabs(out curTab), tabBounds, OnTabClicked, "verticalTabs")
                .AddTextInput(searchFieldBounds, FilterItemsBySearchText, CairoFont.WhiteSmallishText(), "searchField")
                .BeginChildElements(bgBounds)
                    .BeginClip(clipBounds)
                        .AddInset(insetBounds, 3)
                        .AddHandbookStackList(stackListBounds, onLeftClickListElement, shownHandbookPages, "stacklist")
                    .EndClip()
                    .AddVerticalScrollbar(OnNewScrollbarvalueOverviewPage, scrollbarBounds, "scrollbar")
                    .AddSmallButton(Lang.Get("general-back"), OnButtonBack, backButtonBounds, EnumButtonStyle.Normal, EnumTextOrientation.Center, "backButton")
                    .AddSmallButton(Lang.Get("Close Handbook"), OnButtonClose, closeButtonBounds)
                .EndChildElements()
                .Compose()
            ;

            overviewGui.GetScrollbar("scrollbar").SetHeights(
                (float)listHeight, 
                (float)overviewGui.GetHandbookStackList("stacklist").insideBounds.fixedHeight
            );

            overviewGui.GetTextInput("searchField").SetPlaceHolderText("Search...");

            overviewGui.GetVerticalTab("verticalTabs").SetValue(curTab, false);

            overviewGui.FocusElement(overviewGui.GetTextInput("searchField").TabIndex);

            if (curTab == 0) currentCatgoryCode = null;
            else currentCatgoryCode = categoryCodes[curTab - 1];
            FilterItems();
        }

        GuiTab[] genTabs(out int curTab)
        {
            GuiTab[] tabs = new GuiTab[categoryCodes.Count + 1];

            tabs[0] = new GuiTab()
            {
                DataInt = 0,
                Name = Lang.Get("handbook-category-everything")
            };

            curTab = 0;

            for (int i = 1; i < tabs.Length; i++)
            {
                tabs[i] = new GuiTab()
                {
                    DataInt = i,
                    Name = Lang.Get("handbook-category-" + categoryCodes[i - 1])
                };

                if (currentCatgoryCode == categoryCodes[i - 1])
                {
                    curTab = i;
                }
            }

            return tabs;
        }


        private void OnTabClicked(int index, GuiTab tab)
        {
            if (index == 0) currentCatgoryCode = null;
            else currentCatgoryCode = categoryCodes[index - 1];

            FilterItems();
        }

        void initDetailGui() { 
            ElementBounds textBounds = ElementBounds.Fixed(9, 45, 500, 30 + listHeight + 17);
            
            ElementBounds clipBounds = textBounds.ForkBoundingParent();
            ElementBounds insetBounds = textBounds.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);

            ElementBounds scrollbarBounds = clipBounds.CopyOffsetedSibling(textBounds.fixedWidth + 7, -6, 0, 6).WithFixedWidth(20);

            ElementBounds closeButtonBounds = ElementBounds
                .FixedSize(0, 0)
                .FixedUnder(clipBounds, 2 * 5 + 5)
                .WithAlignment(EnumDialogArea.RightFixed)
                .WithFixedPadding(20, 4)
                .WithFixedAlignmentOffset(-11, 1)
            ;
            ElementBounds backButtonBounds = ElementBounds
                .FixedSize(0, 0)
                .FixedUnder(clipBounds, 2 * 5 + 5)
                .WithAlignment(EnumDialogArea.LeftFixed)
                .WithFixedPadding(20, 4)
                .WithFixedAlignmentOffset(4, 1)
            ;
            ElementBounds overviewButtonBounds = ElementBounds
                .FixedSize(0, 0)
                .FixedUnder(clipBounds, 2 * 5 + 5)
                .WithAlignment(EnumDialogArea.CenterFixed)
                .WithFixedPadding(20, 4)
                .WithFixedAlignmentOffset(0, 1)
            ;

            ElementBounds bgBounds = insetBounds.ForkBoundingParent(5, 40, 36, 52).WithFixedPadding(GuiStyle.ElementToDialogPadding / 2);
            bgBounds.WithChildren(insetBounds, textBounds, scrollbarBounds, backButtonBounds, closeButtonBounds);

            BrowseHistoryElement curPage = browseHistory.Peek();
            float posY = curPage.PosY;

            // 3. Finally Dialog
            ElementBounds dialogBounds = bgBounds.ForkBoundingParent().WithAlignment(EnumDialogArea.None).WithAlignment(EnumDialogArea.CenterFixed).WithFixedPosition(0, 70);
            //dialogBounds.Code = "dialogbounds";

            RichTextComponentBase[] cmps = curPage.Page.GetPageText(capi, allstacks, OpenDetailPageFor);

            int curTab;
            ElementBounds tabBounds = ElementBounds.Fixed(-200, 35, 200, 545);

            detailViewGui?.Dispose();
            detailViewGui = capi.Gui
                .CreateCompo("handbook-detail", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(Lang.Get("Survival Handbook"), OnTitleBarClose)
                .AddVerticalTabs(genTabs(out curTab), tabBounds, OnDetailViewTabClicked, "verticalTabs")
                .BeginChildElements(bgBounds)
                    .BeginClip(clipBounds)
                        .AddInset(insetBounds, 3)
                        .AddRichtext(cmps, textBounds, "richtext")
                    .EndClip()
                    .AddVerticalScrollbar(OnNewScrollbarvalueDetailPage, scrollbarBounds, "scrollbar")
                    .AddSmallButton(Lang.Get("general-back"), OnButtonBack, backButtonBounds)
                    .AddSmallButton(Lang.Get("handbook-overview"), OnButtonOverview, overviewButtonBounds)
                    .AddSmallButton(Lang.Get("general-close"), OnButtonClose, closeButtonBounds)
                .EndChildElements()
                .Compose()
            ;

            GuiElementRichtext richtextelem = detailViewGui.GetRichtext("richtext");
            detailViewGui.GetScrollbar("scrollbar").SetHeights(
                (float)listHeight, (float)richtextelem.Bounds.fixedHeight
            );
            detailViewGui.GetScrollbar("scrollbar").CurrentYPosition = posY;
            OnNewScrollbarvalueDetailPage(posY);

            detailViewGui.GetVerticalTab("verticalTabs").SetValue(curTab, false);

            
        }

        private void OnDetailViewTabClicked(int t1, GuiTab t2)
        {
            browseHistory.Clear();
            OnTabClicked(t1, t2);
        }

        private bool OnButtonOverview()
        {
            browseHistory.Clear();
            return true;
        }

        public bool OpenDetailPageFor(string pageCode)
        {
            capi.Gui.PlaySound("menubutton_press");

            int num;
            if (pageNumberByPageCode.TryGetValue(pageCode, out num)) {
                GuiHandbookPage elem = allHandbookPages[num];
                if (browseHistory.Count > 0 && elem == browseHistory.Peek().Page) return true;

                browseHistory.Push(new BrowseHistoryElement()
                {
                    Page = elem,
                    PosY = 0
                });
                initDetailGui();
                return true;
            }

            return false;
        }


        private bool OnButtonBack()
        {
            if (browseHistory.Count == 0) return true;

            browseHistory.Pop();
            if (browseHistory.Count > 0)
            {
                if (browseHistory.Peek().SearchText != null)
                {
                    Search(browseHistory.Peek().SearchText);
                }
                else
                {
                    initDetailGui();
                }
            }

            return true;
        }

        private void onLeftClickListElement(int index)
        {
            browseHistory.Push(new BrowseHistoryElement() {
                Page = shownHandbookPages[index]
            });
            initDetailGui();
        }



        private void OnNewScrollbarvalueOverviewPage(float value)
        {
            GuiElementHandbookList stacklist = overviewGui.GetHandbookStackList("stacklist");

            stacklist.insideBounds.fixedY = 3 - value;
            stacklist.insideBounds.CalcWorldBounds();
        }

        private void OnNewScrollbarvalueDetailPage(float value)
        {
            GuiElementRichtext richtextElem = detailViewGui.GetRichtext("richtext");
            richtextElem.Bounds.fixedY = 3 - value;
            richtextElem.Bounds.CalcWorldBounds();

            browseHistory.Peek().PosY = detailViewGui.GetScrollbar("scrollbar").CurrentYPosition;
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }

        private bool OnButtonClose()
        {
            TryClose();
            return true;
        }

        public override void OnGuiOpened()
        {
            initOverviewGui();
            base.OnGuiOpened();
        }

        public override void OnGuiClosed()
        {
            browseHistory.Clear();
            overviewGui.GetTextInput("searchField").SetValue("");

            base.OnGuiClosed();
        }




        private HashSet<string> initCustomPages()
        {
            List<GuiHandbookTextPage> textpages = capi.Assets.GetMany<GuiHandbookTextPage>(capi.Logger, "config/handbook").OrderBy(pair => pair.Key.ToString()).Select(pair => pair.Value).ToList();
            HashSet<string> categoryCodes = new HashSet<string>();

            foreach (var val in textpages)
            {
                val.Init(capi);
                allHandbookPages.Add(val);
                pageNumberByPageCode[val.PageCode] = val.PageNumber = allHandbookPages.Count - 1;

                categoryCodes.Add(val.CategoryCode);
            }

            return categoryCodes;
        }

        public void InitStackCacheAndStacks()
        {
            List<ItemStack> allstacks = new List<ItemStack>();

            //HashSet<AssetLocation> groupedBlocks = new HashSet<AssetLocation>();
            //HashSet<AssetLocation> groupedItems = new HashSet<AssetLocation>();
            //Dictionary<string, GuiHandbookGroupedItemstackPage> groupedPages = new Dictionary<string, GuiHandbookGroupedItemstackPage>();


            foreach (CollectibleObject obj in capi.World.Collectibles)
            {
                List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                if (stacks == null) continue;

   
                //string[] groups = obj.Attributes?["handbook"]?["groupBy"]?.AsStringArray(null);
                //string[] groupednames = obj.Attributes?["handbook"]?["groupedName"]?.AsStringArray(null);

                foreach (ItemStack stack in stacks)
                {
                    allstacks.Add(stack);

                    /*if (groups != null && groupednames != null) - don't know how to do this right. The detail page also kind of needs to be a slideshow or multi-page thing? meh. :/
                    {
                        bool alreadyAdded = stack.Class == EnumItemClass.Block ? groupedBlocks.Contains(stack.Collectible.Code) : groupedItems.Contains(stack.Collectible.Code);

                        if (!alreadyAdded)
                        {
                            GroupedHandbookStacklistElement elem;
                            if (groupedPages.TryGetValue(stack.Class + "-" + groups[0], out elem))
                            {
                                elem.Stacks.Add(stack);
                                pageNumberByPageCode[HandbookStacklistElement.PageCodeForCollectible(stack.Collectible)] = elem.PageNumber;
                            } else
                            {

                                elem = new GroupedHandbookStacklistElement()
                                {
                                    TextCache = groupednames == null || groupednames.Length == 0 ? stack.GetName() : Lang.Get(groupednames[0]),
                                    Name = groupednames == null || groupednames.Length == 0 ? stack.GetName() : Lang.Get(groupednames[0]),
                                    Visible = true
                                };

                                elem.Stacks.Add(stack);

                                listElements.Add(elem);
                                pageNumberByPageCode[HandbookStacklistElement.PageCodeForCollectible(stack.Collectible)] = elem.PageNumber = listElements.Count - 1;
                                listedListElements.Add(elem);

                                groupedPages[stack.Class +"-"+ groups[0]] = elem;
                            }

                            if (stack.Class == EnumItemClass.Block)
                            {
                                groupedBlocks.Add(stack.Collectible.Code);
                            } else
                            {
                                groupedItems.Add(stack.Collectible.Code);
                            }
                        }
                    }
                    else*/
                    {                       
                        GuiHandbookItemStackPage elem = new GuiHandbookItemStackPage(capi, stack)
                        {
                            Visible = true
                        };

                        allHandbookPages.Add(elem);
                        pageNumberByPageCode[elem.PageCode] = elem.PageNumber = allHandbookPages.Count - 1;
                    }
                }
            }

            this.allstacks = allstacks.ToArray();
        }

        public void Search(string text)
        {
            currentCatgoryCode = null;
            SingleComposer = overviewGui;
            overviewGui.GetTextInput("searchField").SetValue(text);

            if (browseHistory.Count > 0 && browseHistory.Peek().SearchText == text) return;

            capi.Gui.PlaySound("menubutton_press");

            browseHistory.Push(new BrowseHistoryElement()
            {
                Page = null,
                SearchText = text,
                PosY = 0
            });

        }


        private void FilterItemsBySearchText(string text)
        {
            currentSearchText = text;
            FilterItems();
        }

        

        public void FilterItems() 
        { 
            string text = currentSearchText?.ToLowerInvariant();
            string[] texts = text == null ? new string[0] : text.Split(new string[] { " or " }, StringSplitOptions.RemoveEmptyEntries).OrderBy(str => str.Length).ToArray();

            List<WeightedHandbookPage> foundPages = new List<WeightedHandbookPage>();

            shownHandbookPages.Clear();

            for (int i = 0; i < allHandbookPages.Count; i++)
            {
                GuiHandbookPage page = allHandbookPages[i];
                if (currentCatgoryCode != null && page.CategoryCode != currentCatgoryCode) continue;

                float weight = 1;
                bool skip = texts.Length > 0;

                for (int j = 0; j < texts.Length; j++)
                {
                    weight = page.TextMatchWeight(texts[j]);
                    if (weight > 0) { skip = false; break; }
                }
                if (skip) continue;

                foundPages.Add(new WeightedHandbookPage() { Page = page, Weight = weight });
            }

            foreach (var val in foundPages.OrderByDescending(wpage => wpage.Weight))
            {
                shownHandbookPages.Add(val.Page);
            }

            GuiElementHandbookList stacklist = overviewGui.GetHandbookStackList("stacklist");
            stacklist.CalcTotalHeight();
            overviewGui.GetScrollbar("scrollbar").SetHeights(
                (float)listHeight, (float)stacklist.insideBounds.fixedHeight
            );
        }

        public override void OnRenderGUI(float deltaTime)
        {
            if (browseHistory.Count == 0 || browseHistory.Peek().SearchText != null)
            {
                SingleComposer = overviewGui;
            }
            else
            {
                SingleComposer = detailViewGui;
            }

            if (SingleComposer == overviewGui)
            {
                overviewGui.GetButton("backButton").Enabled = browseHistory.Count > 0;
            }

            base.OnRenderGUI(deltaTime);
        }


        public override bool PrefersUngrabbedMouse => true;

        public override bool CaptureAllInputs()
        {
            return false;
        }

        public override void Dispose()
        {
            overviewGui?.Dispose();
            detailViewGui?.Dispose();
        }


    }
}
