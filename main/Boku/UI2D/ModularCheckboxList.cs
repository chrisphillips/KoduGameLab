// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.


using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Storage;

using Boku.Audio;
using Boku.Base;
using Boku.Common;
using Boku.Fx;
using Boku.Input;
using Boku.Common.Gesture;

namespace Boku.UI2D
{
    /// <summary>
    /// New look for a list of checkbox elements done without prerendering to a texture.
    /// </summary>
    public class ModularCheckboxList : INeedsDeviceReset
    {
        public delegate void UICheckboxListEvent(ModularCheckboxList list);

        UICheckboxListEvent onExit = null;      // Called when exiting the list.
        UICheckboxListEvent onChange = null;    // Called whenever any element's state is changed.

        Effect effect = null;
        UI2D.Shared.GetFont Font = UI2D.Shared.GetGameFont24;

        float tileBorder = 0.125f;
        int checkboxSize = 30;
        int margin = 10;        // Space to left of checkbox and right of text.
        int gap = 8;            // Space between checkbox and text.

        CommandMap commandMap = new CommandMap("ModularCheckboxList");

        public class CheckboxItem
        {
            #region Members

            public static Color selectedTextColor = new Color(20, 20, 20);
            public static Color unselectedTextColor = new Color(0, 255, 12);

            ModularCheckboxList parent = null;
            Object obj = null;      // Whatever the user wants.

            bool check = false;     // Current state.
            float checkLit = 0.0f;  // 0..1 value whether or not the checkbox is lit.

            string keyText;         // String which is fed to localization to get text to display.
            string localizedText;   // Localized text which we display.
            Vector4 textColor = unselectedTextColor.ToVector4();    // Displayed color.
            Vector4 _textColor = unselectedTextColor.ToVector4();   // Twitch target color.
            float barAlpha = 0.0f;                                  // Display alpha for highlight bar.
            float _barAlpha = 0.0f;                                 // Twitch target alpha.

            bool selected = false;  // Is this the "in focus" element?

            AABB2D uvBoundingBox = new AABB2D();                    // Needs to be filled in when rt is refreshed.

            #endregion

            #region Accessors

            public bool Selected
            {
                get { return selected; }
                set
                {
                    if (selected != value)
                    {
                        selected = value;
                        if (selected)
                        {
                            // Twitch to selected colors.
                            float time = 0.2f;
                            {
                                _textColor = selectedTextColor.ToVector4();
                                TwitchManager.Set<Vector4> set = delegate(Vector4 val, Object param) { textColor = val; parent.dirty = true; };
                                TwitchManager.CreateTwitch<Vector4>(textColor, _textColor, set, time, TwitchCurve.Shape.EaseOut);
                            }
                            {
                                _barAlpha = 1.0f;
                                TwitchManager.Set<float> set = delegate(float val, Object param) { barAlpha = val; parent.dirty = true; };
                                TwitchManager.CreateTwitch<float>(barAlpha, _barAlpha, set, time, TwitchCurve.Shape.EaseOut);
                            }
                        }
                        else
                        {
                            // Twitch to unselected colors.
                            float time = 0.2f;
                            {
                                _textColor = unselectedTextColor.ToVector4();
                                TwitchManager.Set<Vector4> set = delegate(Vector4 val, Object param) { textColor = val; parent.dirty = true; };
                                TwitchManager.CreateTwitch<Vector4>(textColor, _textColor, set, time, TwitchCurve.Shape.EaseIn);
                            }
                            {
                                _barAlpha = 0.0f;
                                TwitchManager.Set<float> set = delegate(float val, Object param) { barAlpha = val; parent.dirty = true; };
                                TwitchManager.CreateTwitch<float>(barAlpha, _barAlpha, set, time, TwitchCurve.Shape.EaseIn);
                            }
                        }
                    }
                }
            }   // end of Selected accessor

            /// <summary>
            /// Whether or not this item is checked.
            /// </summary>
            public bool Check
            {
                get { return check; }
                set
                {
                    if(check != value)
                    {
                        check = value;
                        if(check)
                        {
                            TwitchManager.Set<float> set = delegate(float val, Object param) { checkLit = val; };
                            TwitchManager.CreateTwitch<float>(checkLit, 1.0f, set, 0.05f, TwitchCurve.Shape.EaseIn);
                        }
                        else
                        {
                            TwitchManager.Set<float> set = delegate(float val, Object param) { checkLit = val; };
                            TwitchManager.CreateTwitch<float>(checkLit, 0.0f, set, 0.05f, TwitchCurve.Shape.EaseIn);
                        }
                    }
                }
            }

            public float BarAlpha
            {
                get { return barAlpha; }
            }

            public string LocalizedText
            {
                get { return localizedText; }
                //set { localizedText = TextHelper.FilterInvalidCharacters(value); }
            }

            public string KeyText
            {
                get { return keyText; }
            }

            public AABB2D UVBoundingBox
            {
                get { return uvBoundingBox; }
                set { uvBoundingBox = value; }
            }

            // User definable object ref.
            public Object Obj
            {
                get { return obj; }
                set { obj = value; }
            }

            /// <summary>
            /// 0..1 How "lit" is the checkbox.
            /// </summary>
            public float LitValue
            {
                get { return checkLit; }
            }

            public Color TextColor
            {
                get { return new Color(textColor); }
            }

            #endregion


            #region Public

            /// <summary>
            /// 
            /// </summary>
            /// <param name="keyText">This is the text which is fed into localization.  We also use this to id and find this item.</param>
            /// <param name="check"></param>
            /// <param name="parent"></param>
            /// <param name="obj"></param>
            public CheckboxItem(string keyText, bool check, ModularCheckboxList parent, object obj = null)
            {
                this.keyText = keyText;
                localizedText = Strings.Localize(keyText);
                if (localizedText == "")
                {
                    //Debug.Assert(false, "Why can't we localize this?");
                    // Punt and display the key.
                    localizedText = keyText;
                }
                localizedText = TextHelper.FilterInvalidCharacters(localizedText);
                this.check = check;
                this.parent = parent;
                this.obj = obj;

                if (check)
                {
                    checkLit = 1.0f;
                }
            }

            #endregion
        }   // end of class CheckboxItem

        Texture2D normalMap = null;

        Texture2D whiteHighlight = null;
        Texture2D greenBar = null;
        Texture2D checkboxLit = null;
        Texture2D checkboxUnlit = null;
        Texture2D radioButtonLit = null;
        Texture2D radioButtonUnlit = null;

        int curIndex = 0;   // The currently selected option.

        int w = 0;  // Size in pixels
        int h = 0;

        // Properties for the underlying 9-grid geometry.
        float width;
        float height;
        float edgeSize = 0.06f;

        Base9Grid geometry = null;
        Matrix worldMatrix = Matrix.Identity;
        Matrix invWorldMatrix = Matrix.Identity;

        bool active = false;

        List<CheckboxItem> itemList = null; // The list of items.

        bool dirty = true;              // Used?

        bool allExclusive = false;          // Treat all items as radio buttons.
        bool exclusiveFirstItem = false;    // Treat 1st item as a radio button relative to all other entries.

        bool useRtCoords = false;           // If the menu is being rendered into a rendertarget this should be set to true.
                                                    // Currently for MainMenu this is true, for MiniHub this is false.

        AABB2D changeBox = new AABB2D();    // Mouse hit box for "change" button.
        AABB2D backBox = new AABB2D();      // Mouse hit box for "back" button.

        #region Accessors

        public UICheckboxListEvent OnExit
        {
            set { onExit = value; }
        }

        public UICheckboxListEvent OnChange
        {
            set { onChange = value; }
        }

        public bool Active
        {
            get { return active; }
        }

        public void Activate(bool useRtCoords)
        {
            this.useRtCoords = useRtCoords;

            if (active != true)
            {
                active = true;

                HelpOverlay.Push("ModularCheckboxList");
                CommandStack.Push(commandMap);
                ApplyExclusiveFirstItemFiltering();
            }
        }   // end of Activate()

        public void Deactivate()
        {
            if (active)
            {
                active = false;

                HelpOverlay.Pop();
                CommandStack.Pop(commandMap);
                if (onExit != null)
                {
                    onExit(this);
                }
            }
        }   // end of Deactivate()

        public int CurIndex
        {
            get { return curIndex; }
            set
            {
                if (curIndex != value)
                {
                    curIndex = value;
                    dirty = true;
                }
            }
        }

        /// <summary>
        /// Returns the text for the currently selected item.
        /// </summary>
        public string CurLocalizedString
        {
            get { return itemList[curIndex].LocalizedText; }
        }

        /// <summary>
        /// The checked state of the currently selected item.
        /// </summary>
        public bool CurChecked
        {
            get { return itemList[curIndex].Check; }
            set { itemList[curIndex].Check = value; }
        }

        public Matrix WorldMatrix
        {
            get { return worldMatrix; }
            set
            {
                worldMatrix = value;
                invWorldMatrix = Matrix.Invert(worldMatrix);
            }
        }

        public int NumItems
        {
            get { return itemList.Count; }
        }

        /// <summary>
        /// When true, treats all the items as mutually exclusive, ie radio buttons.
        /// </summary>
        public bool AllExclusive
        {
            get { return allExclusive; }
            set { allExclusive = value; }
        }

        /// <summary>
        /// Special case code for use with the level loading screens.  When this
        /// is set to true it assumes that the first item (all) is treated special.
        /// If "all" is selected, all the other boxes are cleared.
        /// If any other box is selected, "all" is cleared.
        /// If all other boxes are cleared, "all" is automatically selected.
        /// </summary>
        public bool ExclusiveFirstItem
        {
            get { return exclusiveFirstItem; }
            set { exclusiveFirstItem = value; }
        }

        #endregion

        // c'tor
        public ModularCheckboxList()
        {
            itemList = new List<CheckboxItem>();
        }

        /// <summary>
        /// Gets an item based on its index in the list.
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public CheckboxItem GetItem(int i)
        {
            Debug.Assert(i < itemList.Count && i >= 0);
            return itemList[i];
        }

        /// <summary>
        /// Adds a new Checkbox item.
        /// </summary>
        /// <param name="text">Text key which will be localized for label on checkbox</param>
        /// <param name="check">Initial state of checkbox.</param>
        public void AddItem(string keyText, bool check)
        {
            CheckboxItem item = new CheckboxItem(keyText, check, this);
            itemList.Add(item);
            if (itemList.Count == 1)
            {
                item.Selected = true;
            }

            dirty = true;
        }   // end of ModularCheckboxList AddItem()

        /// <summary>
        /// Adds a new Checkbox item.
        /// </summary>
        /// <param name="keyText">Text key which will be localized for label on checkbox</param>
        /// <param name="check">Initial state of checkbox.</param>
        /// <param name="obj">User defined object ref.</param>
        public void AddItem(string keyText, bool check, object obj)
        {
            CheckboxItem item = new CheckboxItem(keyText, check, this, obj);
            itemList.Add(item);
            if (itemList.Count == 1)
            {
                item.Selected = true;
            }

            dirty = true;
        }   // end of ModularCheckboxList AddText()

        /// <summary>
        /// Inserts a new item at the given index.
        /// </summary>
        /// <param name="keyText">Text key which will be localized for label on checkbox</param>
        /// <param name="check">Initial state of checkbox.</param>
        /// <param name="index">Index for new item</param>
        public void InsertText(string keyText, bool check, int index)
        {
            CheckboxItem item = new CheckboxItem(keyText, check, this);

            // Move everything below the new entry down one space.
            itemList.Add(itemList[itemList.Count - 1]);
            for (int i = itemList.Count - 1; i > index; i--)
            {
                itemList[i] = itemList[i - 1];
            }
            itemList[index] = item;

            InitDeviceResources(BokuGame.bokuGame.GraphicsDevice);

            dirty = true;
        }   // end of InsertText()

        /// <summary>
        /// Removes the given entry based on its KeyText.
        /// </summary>
        /// <param name="text"></param>
        public void DeleteText(string keyText)
        {
            for (int i = 0; i < itemList.Count; i++)
            {
                if (itemList[i].KeyText == keyText)
                {
                    itemList.RemoveAt(i);
                    break;
                }
            }

            InitDeviceResources(BokuGame.bokuGame.GraphicsDevice);

            dirty = true;
        }   // end of DeleteText()

        void HandleMouseInput(Camera camera)
        {
            if (GamePadInput.ActiveMode != GamePadInput.InputMode.KeyboardMouse) { return; }

            // If the mouse is over the menu, move the selection index to the item under the mouse.
            // On mouse down, make the item (if any) under the mouse the ClickedOnItem.
            // On mouse up, if the mouse is still over the ClickedOnItem, activate it.  If not, just clear ClickedOnItem. 
            Vector2 hitUV = MouseInput.GetHitUV(camera, ref invWorldMatrix, width, height, useRtCoords: useRtCoords);

            // See if we're over anything.  If so, set that item to being selected but only if we've moved the mouse.
            // This prevents the menu from feeling 'broken' if the mouse is over it and the user tries to use
            // the gamepad or keyboard.
            int mouseOverItem = -1;
            for (int i = 0; i < itemList.Count; i++)
            {
                if (itemList[i].UVBoundingBox != null && itemList[i].UVBoundingBox.Contains(hitUV))
                {
                    // Only update the current in-focus element when the mouse moves.
                    if (MouseInput.Position != MouseInput.PrevPosition)
                    {
                        CurIndex = i;
                    }
                    mouseOverItem = i;
                }
            }

            if (MouseInput.Left.WasPressed)
            {
                if (mouseOverItem != -1)
                {
                    MouseInput.ClickedOnObject = itemList[mouseOverItem];
                }
            }
            if (MouseInput.Left.WasReleased)
            {
                // Make sure we're still over the ClickedOnItem.
                if (mouseOverItem != -1 && MouseInput.ClickedOnObject == itemList[mouseOverItem])
                {
                    ToggleState();
                }
            }

            Vector2 hit = MouseInput.PositionVec;
            if (useRtCoords)
            {
                hit = ScreenWarp.ScreenToRT(hit);
            }
            if (changeBox.LeftPressed(hit))
            {
                ToggleState();
            }
            if (backBox.LeftPressed(hit))
            {
                Deactivate();
                Foley.PlayBack();
            }

            // Allow scroll wheel to cycle through elements.
            int wheel = MouseInput.ScrollWheel - MouseInput.PrevScrollWheel;

            if (wheel > 0)
            {
                --curIndex;
                if (curIndex < 0)
                {
                    curIndex = itemList.Count - 1;
                }

                Foley.PlayShuffle();
            }
            else if (wheel < 0)
            {
                ++curIndex;
                if (curIndex >= itemList.Count)
                {
                    curIndex = 0;
                }

                Foley.PlayShuffle();
            }

            // If we click outside of the list, close it treating it as if select was chosen.
            if (MouseInput.Left.WasPressed && MouseInput.ClickedOnObject == null)
            {
                Deactivate();
            }
        }

        void HandleTouchInput(Camera camera)
        {
            if (GamePadInput.ActiveMode != GamePadInput.InputMode.Touch) { return; }

            // Touch input
            // If the touch is over the menu, move the selection index to the item under the mouse.
            // On touch down, make the item (if any) under the touch the ClickedOnItem.
            // On tap, if the touch is still over the ClickedOnItem, activate it.  If not, just clear ClickedOnItem. 
            TouchContact touch = TouchInput.GetOldestTouch();
            if (touch != null)
            {
                Vector2 hitUV = TouchInput.GetHitUV(touch.position, camera, ref invWorldMatrix, width, height, useRtCoords: useRtCoords);

                // See if we're over anything.  If so, set that item to being selected but only if we've moved the mouse.
                // This prevents the menu from feeling 'broken' if the mouse is over it and the user tries to use
                // the gamepad or keyboard.
                int touchItem = -1;
                for (int i = 0; i < itemList.Count; i++)
                {
                    if (itemList[i].UVBoundingBox != null && itemList[i].UVBoundingBox.Contains(hitUV))
                    {
                        // Only update the current in-focus element when the mouse moves.
                        if (true) // touch.position != touch.previousPosition)
                        {
                            CurIndex = i;
                        }
                        touchItem = i;
                    }
                }

                //if ( TouchInput.TapGesture.WasTapped() )
                if (TouchInput.IsTouched)
                {
                    if (touchItem != -1)
                    {
                        touch.TouchedObject = itemList[touchItem];
                    }
                }
                if (TouchGestureManager.Get().TapGesture.WasTapped())
                {
                    // Make sure we're still over the ClickedOnItem.
                    if (touchItem != -1 && touch.TouchedObject == itemList[touchItem])
                    {
                        ToggleState();
                    }
                }

                Vector2 hit = touch.position;
                if (useRtCoords)
                {
                    hit = ScreenWarp.ScreenToRT(hit);
                }

                if (changeBox.Touched(touch, hit))
                {
                    ToggleState();
                }
                if (backBox.Touched(touch, hit))
                {
                    Deactivate();
                    Foley.PlayBack();
                }

                // Allow Swipeto cycle through elements.
                // Allow scroll wheel to cycle through elements.
                SwipeGestureRecognizer swipeGesture = TouchGestureManager.Get().SwipeGesture;
                if (swipeGesture.WasSwiped())
                {

                    if (swipeGesture.SwipeDirection == Boku.Programming.Directions.South)
                    {
                        curIndex -= 6;
                        if (curIndex < 0)
                        {
                            curIndex = itemList.Count - 1;
                        }
                        Foley.PlayShuffle();
                    }
                    else if (swipeGesture.SwipeDirection == Boku.Programming.Directions.North)
                    {
                        curIndex += 6;
                        if (curIndex > (itemList.Count - 1))
                        {
                            curIndex = 0;
                        }
                        Foley.PlayShuffle();
                    }
                }

                // If we click outside of the list, close it treating it as if select was chosen.
                //if (TouchInput.TapGesture.WasTapped() && touch.touchedObject == null)
                if (TouchInput.WasTouched && touch.TouchedObject == null)
                {
                    Deactivate();
                }
            }
        }

        void HandleGamePadInput(Camera camera)
        {
            if (GamePadInput.ActiveMode != GamePadInput.InputMode.GamePad) { return; }

            GamePadInput pad = GamePadInput.GetGamePad0();

            if (Actions.Select.WasPressed)
            {
                Actions.Select.ClearAllWasPressedState();

                ToggleState();
            }

            if (Actions.Cancel.WasPressed)
            {
                Actions.Cancel.ClearAllWasPressedState();

                // Shut down.
                Deactivate();
                Foley.PlayBack();

                return;
            }

            // Handle input changes here.
            if (Actions.ComboDown.WasPressedOrRepeat)
            {
                ++curIndex;
                if (curIndex >= itemList.Count)
                {
                    curIndex = 0;
                }

                Foley.PlayShuffle();
            }

            if (Actions.ComboUp.WasPressedOrRepeat)
            {
                --curIndex;
                if (curIndex < 0)
                {
                    curIndex = itemList.Count - 1;
                }

                Foley.PlayShuffle();
            }

            // Don't let anyone else steal input while we're active.
            GamePadInput.ClearAllWasPressedState();
        }

        public void Update(Camera camera, ref Matrix parentMatrix)
        {
            // Check for input.
            if (active && itemList.Count > 1)
            {
                HandleMouseInput(camera);
                HandleTouchInput(camera);
                HandleGamePadInput(camera);

                // Ensure that the selected state of all items is correct.
                for (int i = 0; i < itemList.Count; i++)
                {
                    itemList[i].Selected = i == CurIndex;
                }

                if (dirty)
                {
                    // Recalc size of box.
                    h = itemList.Count * Font().LineSpacing;
                    w = 0;
                    for (int i = 0; i < itemList.Count; i++)
                    {
                        w = Math.Max(w, 2 * margin + checkboxSize + gap + (int)Font().MeasureString(itemList[i].LocalizedText).X);
                    }

                    if (geometry != null)
                    {
                        BokuGame.Unload(geometry);
                        geometry = null;
                    }
                    width = w / 96.0f + 2.0f * tileBorder;
                    height = h / 96.0f + 2.0f * tileBorder;
                    geometry = new Base9Grid(width, height, edgeSize);

                    geometry.InitDeviceResources(BokuGame.bokuGame.GraphicsDevice);

                    dirty = false;
                }
            }

        }   // end of ModularCheckboxList Update()

        /// <summary>
        /// Toggles the state of the current item.
        /// </summary>
        void ToggleState()
        {
            // Toggle current item state.
            if (!allExclusive)
            {
                itemList[curIndex].Check = !itemList[curIndex].Check;
            }
            else
            {
                // Clear everything and set the current item.
                for (int i = 0; i < itemList.Count; i++)
                {
                    itemList[i].Check = i == curIndex;
                }
            }

            ApplyExclusiveFirstItemFiltering();

            if (onChange != null)
            {
                onChange(this);
            }

            Foley.PlayPressA();

        }   // end of ToggleState()

        void ApplyExclusiveFirstItemFiltering()
        {
            if (exclusiveFirstItem)
            {
                // If "all" was checked, clear all others.
                if (curIndex == 0 && itemList[curIndex].Check)
                {
                    for (int i = 1; i < itemList.Count; i++)
                    {
                        itemList[i].Check = false;
                    }
                }

                // If anything else was checked, clear "all".
                if (curIndex != 0 && itemList[curIndex].Check)
                {
                    itemList[0].Check = false;
                }

                // If all others unchecked, check "all".
                bool allClear = true;
                for (int i = 1; i < itemList.Count; i++)
                {
                    if (itemList[i].Check)
                    {
                        allClear = false;
                        break;
                    }
                }
                if (allClear)
                {
                    itemList[0].Check = true;
                }
            }
        }   // end of ApplyExclusiveFiltering()


        public void Render(Camera camera)
        {
            if (Active && geometry != null)
            {
                // Black background.
                effect.CurrentTechnique = effect.Techniques["NormalMappedNoTexture"];

                effect.Parameters["WorldMatrix"].SetValue(worldMatrix);
                effect.Parameters["WorldViewProjMatrix"].SetValue(worldMatrix * camera.ViewProjectionMatrix);

                effect.Parameters["Alpha"].SetValue(1.0f);
                effect.Parameters["DiffuseColor"].SetValue(new Vector4(0, 0, 0, 1));
                effect.Parameters["SpecularColor"].SetValue(new Vector4(0.2f, 0.2f, 0.2f, 1.0f));
                effect.Parameters["SpecularPower"].SetValue(32.0f);

                effect.Parameters["NormalMap"].SetValue(normalMap);

                geometry.Render(effect);


                ScreenSpaceQuad quad = ScreenSpaceQuad.GetInstance();

                Vector3 upperLeftCorner = worldMatrix.Translation;
                upperLeftCorner.X -= geometry.Width / 2.0f;
                upperLeftCorner.Y += geometry.Height / 2.0f;

                upperLeftCorner.X += tileBorder;
                upperLeftCorner.Y -= tileBorder / 2.0f;

                Point loc = camera.WorldToScreenCoords(upperLeftCorner);
                Vector2 pos = new Vector2(loc.X, loc.Y);

                loc = camera.WorldToScreenCoords(2.0f * worldMatrix.Translation - upperLeftCorner);
                Vector2 size = new Vector2(loc.X, loc.Y) - pos;

                int lineSpacing = Font().LineSpacing;

                // Render highlight.
                quad.Render(whiteHighlight, new Vector4(1.0f, 1.0f, 1.0f, 0.3f), pos + new Vector2(-4, 0), new Vector2(w + 6, w / 2.0f), "TexturedRegularAlpha");

                // Render the green bar().
                for (int i = 0; i < itemList.Count; i++)
                {
                    if (itemList[i].BarAlpha > 0.0)
                    {
                        quad.Render(greenBar, new Vector4(1.0f, 1.0f, 1.0f, itemList[i].BarAlpha), pos + new Vector2(0, 2 + i * lineSpacing), new Vector2(w, lineSpacing), "TexturedRegularAlpha");
                    }
                }

                // Render the text.
                SpriteBatch batch = UI2D.Shared.SpriteBatch;
                batch.Begin();
                for (int i = 0; i < itemList.Count; i++)
                {
                    Vector2 position = pos + new Vector2(margin + checkboxSize + gap, i * lineSpacing);
                    TextHelper.DrawString(Font, itemList[i].LocalizedText, position, itemList[i].TextColor);
                    // Calc bounds in UV space.
                    Vector2 min = new Vector2(0, i * lineSpacing);
                    Vector2 max = min + new Vector2(w, lineSpacing);
                    min /= size;
                    max /= size;
                    itemList[i].UVBoundingBox.Set(min, max);
                }
                batch.End();

                // Render the checkboxes.
                for (int i = 0; i < itemList.Count; i++)
                {
                    float a = itemList[i].LitValue;
                    
                    // If not fully lit.
                    if (a < 1.0f)
                    {
                        quad.Render(AllExclusive ? radioButtonUnlit : checkboxUnlit, pos + new Vector2(margin, 6 + i * lineSpacing), new Vector2(checkboxSize), "TexturedRegularAlpha");
                    }

                    // If lit at all.
                    if (a > 0.0f)
                    {
                        quad.Render(AllExclusive ? radioButtonLit : checkboxLit, new Vector4(1, 1, 1, a), pos + new Vector2(margin, 6 + i * lineSpacing), new Vector2(checkboxSize), "TexturedRegularAlpha");
                    }
                }

                if (GamePadInput.ActiveMode == GamePadInput.InputMode.GamePad)
                {
                    // Render help.
                    string str = "<a> " + Strings.Localize("saveLevelDialog.change") + "\n<b> " + Strings.Localize("saveLevelDialog.back");
                    TextBlob blob = new TextBlob(UI2D.Shared.GetGameFont24, str, 400);
                    pos += size;
                    pos.X += 16;
                    pos.Y -= 2.0f * blob.TotalSpacing;
                    blob.RenderWithButtons(pos, Color.White);

                    // Mouse hit boxes for help.
                    changeBox.Set(pos, pos + new Vector2(blob.GetLineWidth(0), blob.TotalSpacing));
                    backBox.Set(pos + new Vector2(0, blob.TotalSpacing), pos + new Vector2(0, blob.TotalSpacing) + new Vector2(blob.GetLineWidth(1), blob.TotalSpacing));
                }

            }   // end if active

        }   // end of ModularCheckboxList Render()

        /// <summary>
        /// This sets the current index on the matching text line.  If no
        /// matching line is found, the current index is not changed.
        /// </summary>
        /// <param name="keyText"></param>
        public void SetValue(string keyText)
        {
            for (int i = 0; i < itemList.Count; i++)
            {
                if (itemList[i].KeyText == keyText)
                {
                    curIndex = i;
                    dirty = true;
                    break;
                }
            }
        }   // end of ModularCheckboxList SetValue()

        /// <summary>
        /// Returns the index associated with the text. 
        /// Returns -1 if not found.
        /// </summary>
        /// <param name="keyText"></param>
        /// <returns></returns>
        public int GetIndex(string keyText)
        {
            int result = -1;
            for (int i = 0; i < itemList.Count; i++)
            {
                if (itemList[i].KeyText == keyText)
                {
                    result = i;
                    break;
                }
            }

            return result;
        }   // end of GetIndex()

        /// <summary>
        /// Returns the index associated with the object. 
        /// Returns -1 if not found.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public int GetIndex(Object obj)
        {
            int result = -1;
            for (int i = 0; i < itemList.Count; i++)
            {
                if (itemList[i].Obj == obj)
                {
                    result = i;
                    break;
                }
            }

            return result;
        }   // end of GetIndex()

        /// <summary>
        /// Call any OnExit delegates.
        /// </summary>
        public void CallOnExit()
        {
            if (onExit != null)
            {
                onExit(this);
            }
        }

        public void LoadContent(bool immediate)
        {
            // Init the effect.
            if (effect == null)
            {
                effect = BokuGame.Load<Effect>(BokuGame.Settings.MediaPath + @"Shaders\UI2D");
                ShaderGlobals.RegisterEffect("UI2D", effect);
            }

            if (whiteHighlight == null)
            {
                whiteHighlight = BokuGame.Load<Texture2D>(BokuGame.Settings.MediaPath + @"Textures\HelpCard\GlassTileHighlight");
            }
            if (greenBar == null)
            {
                greenBar = BokuGame.Load<Texture2D>(BokuGame.Settings.MediaPath + @"Textures\GridElements\GreenBar");
            }
            if (checkboxLit == null)
            {
                checkboxLit = BokuGame.Load<Texture2D>(BokuGame.Settings.MediaPath + @"Textures\GridElements\CheckboxLit");
            }
            if (checkboxUnlit == null)
            {
                checkboxUnlit = BokuGame.Load<Texture2D>(BokuGame.Settings.MediaPath + @"Textures\GridElements\CheckboxUnlit");
            }
            if (radioButtonLit == null)
            {
                radioButtonLit = BokuGame.Load<Texture2D>(BokuGame.Settings.MediaPath + @"Textures\GridElements\IndicatorLit");
            }
            if (radioButtonUnlit == null)
            {
                radioButtonUnlit = BokuGame.Load<Texture2D>(BokuGame.Settings.MediaPath + @"Textures\GridElements\IndicatorUnlit");
            }

            // Load the normal map texture.
            if (normalMap == null)
            {
                normalMap = BokuGame.Load<Texture2D>(BokuGame.Settings.MediaPath + @"Textures\UI2D\FlatNormalMap");
            }
        }

        public void InitDeviceResources(GraphicsDevice device)
        {
        }

        public void UnloadContent()
        {
            BokuGame.Release(ref effect);
            BokuGame.Release(ref normalMap);
            BokuGame.Release(ref whiteHighlight);
            BokuGame.Release(ref greenBar);
            BokuGame.Release(ref checkboxLit);
            BokuGame.Release(ref checkboxUnlit);
            BokuGame.Release(ref radioButtonLit);
            BokuGame.Release(ref radioButtonUnlit);

            BokuGame.Unload(geometry);
            geometry = null;

        }   // end of ModularCheckboxList UnloadContent()

        /// <summary>
        /// Recreate render targets
        /// </summary>
        /// <param name="graphics"></param>
        public void DeviceReset(GraphicsDevice device)
        {
        }

    }   // end of class ModularCheckboxList

}   // end of namespace Boku.UI2D






