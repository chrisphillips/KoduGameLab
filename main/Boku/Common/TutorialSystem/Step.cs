// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.


using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;

using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Storage;

using Boku.Base;
using Boku.Fx;
using Boku.Common;
using Boku.Common.Xml;

namespace Boku.Common.TutorialSystem
{
    /// <summary>
    /// A single step in a tutorial.  A full tutorial consists of a list of Steps.
    /// </summary>
    public class Step
    {
        public enum Display
        {
            Modal,      // Information is displayed (large window) to the user and the user must hit Continue to move on.
            NonModal,   // Information is displayed to the user at the top of the screen.  The tutorial moves on when
                        // the Step's completion condition is met.
            None,       // Should be an error.
        };

        /// <summary>
        /// Allows steps to be excluded depending on the current input mode.  By default no steps
        /// are excluded.
        /// </summary>
        public enum Exclusions
        {
            None,
            Mouse,
            Gamepad,
            Touch,
            MouseGamepad,   // Excludes both.
            MouseTouch,
            TouchGamepad,
        }

        #region Members

        /// <summary>
        /// Since XML comments don't persist over serialization/deserialization we have
        /// this comment string.  It's not used for anything internally.  It's just 
        /// there to help out people creating tutorials.
        /// </summary>
        string comment = null;

        Display displayMode = Display.None;
        Exclusions exclusion = Exclusions.None;

        TutorialManager.GameMode targetModeGamepad = TutorialManager.GameMode.MainMenu;
        TutorialManager.GameMode targetModeMouse = TutorialManager.GameMode.MainMenu;
        TutorialManager.GameMode targetModeTouch = TutorialManager.GameMode.MainMenu;

        /// <summary>
        /// This is the text displayed on the first line of the non-modal dialog.
        /// </summary>
        public string GoalText = null;
        public string GamepadText = null;
        public string MouseText = null;
        public string TouchText = null;

        // Completion tests for non-modal steps.
        CompletionTest completionTest = null;

        // Actor name to put an arrow over.
        string targetCharacter = null;

        // TODO Need to add some completion criteria for non-modal steps.

        #endregion

        #region Accessors

        public string Comment
        {
            get { return comment; }
            set { comment = value; }
        }

        public Display DisplayMode
        {
            get { return displayMode; }
            set { displayMode = value; }
        }

        public Exclusions Exclusion
        {
            get { return exclusion; }
            set { exclusion = value; }
        }

        public TutorialManager.GameMode TargetModeGamepad
        {
            get { return targetModeGamepad; }
            set { targetModeGamepad = value; }
        }
        public TutorialManager.GameMode TargetModeMouse
        {
            get { return targetModeMouse; }
            set { targetModeMouse = value; }
        }

        public TutorialManager.GameMode TargetModeTouch
        {
            get { return targetModeTouch; }
            set { targetModeTouch = value; }
        }

        public string TargetCharacter
        {
            get { return targetCharacter; }
            set { targetCharacter = value; }
        }

        /// <summary>
        /// Returns the target mode for this step based on the current input mode.
        /// </summary>
        [XmlIgnore]
        public TutorialManager.GameMode TargetMode
        {
            get
            {
                switch(GamePadInput.ActiveMode)
                {
                    case GamePadInput.InputMode.KeyboardMouse:
                        return TargetModeMouse;
                    case GamePadInput.InputMode.Touch:
                        return TargetModeTouch;
                    case GamePadInput.InputMode.GamePad:
                    default:
                        return TargetModeGamepad;
                }
            }
        }

        // Support for localization of text strings.
        public XmlSerializableDictionary<string, string> LocalizedGoalTextDict = null;
        public string OriginalGoalText;
        public string LocalizedGoalText;
        public XmlSerializableDictionary<string, string> LocalizedGamepadTextDict = null;
        public string OriginalGamepadText;
        public string LocalizedGamepadText;
        public XmlSerializableDictionary<string, string> LocalizedMouseTextDict = null;
        public string OriginalMouseText;
        public string LocalizedMouseText;
        public XmlSerializableDictionary<string, string> LocalizedTouchTextDict = null;
        public string OriginalTouchText;
        public string LocalizedTouchText;
        
        /// <summary>
        /// Test used for non-modal steps to determine when they've completed.
        /// </summary>
        public CompletionTest CompletionTest
        {
            get { return completionTest; }
            set { completionTest = value; }
        }
        
        #endregion

        #region Public
        #endregion

        #region Internal
        #endregion

    }   // end of class Step

}   // end of namespace Boku.Common.TutorialSystem
