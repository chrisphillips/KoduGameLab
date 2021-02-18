// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Boku.Base;
using BokuShared;
using Boku.Programming;

using Boku.Common.TutorialSystem;

namespace Boku.Common.Xml
{
    public class ScoreXmlSetting
    {
        public Classification.Colors color;
        public ScoreVisibility visibility;
        public bool persist;

        public string label; //If label exists. then we are labeled.
    }

    public class TouchGUIButtonXmlSetting
    {
        public Classification.Colors color;
        public GUIButton.DisplayType displayType = GUIButton.DisplayType.DT_Solid;
        public string label;
    }

    public class ChangeHistoryEntry
    {
        public string gamertag;
        public DateTime time = DateTime.MinValue;
    }

    public class XmlWorldData : BokuShared.XmlData<XmlWorldData>
    {
        #region Members

        public Guid id = Guid.Empty;
        public string name;             // The level name as displayed to the user.
        public XmlSerializableDictionary<string, string> localizedNameDict = null;
        string originalName;            // Name originally found in file.
        string localizedName;           // Name after localization.
        public string description;
        public XmlSerializableDictionary<string, string> localizedDescriptionDict = null;
        string originalDescription;     // Description originally found in file.
        string localizedDescription;    // Description after localization.
        public UI2D.UIGridElement.Justification descJustification = Boku.UI2D.UIGridElement.Justification.Left;
        public string creator;

        /// <summary>
        /// Level linking - which levels link to this level, and which levels does this level link to.  We don't need 
        /// Genre here, we can assume linked levels always match the genre of the level they're linking from/to.
        /// </summary>
        public Guid? LinkedFromLevel = null;
        public Guid? LinkedToLevel = null;


        /// <summary>
        /// The last modified time of the world. Always expressed in UTC.
        /// </summary>
        public DateTime lastWriteTime = DateTime.MinValue;

        /// <summary>
        /// The version of Kodu that saved this level to disk.
        /// </summary>
        public string BokuVersion = "1.0.0.0";
        public string KCodeVersion = "0";

        /// <summary>
        /// If set, this field's value will be used as the level's timestamp the next time it is written to disk.
        /// After use, this field is cleared. Value must be UTC.
        /// 
        /// (****) I _think_ this is being used so that downloading a world doesn't cause its date to change.
        /// Sadly this comment just says _what_ is happening, not _why_.
        /// </summary>
        [XmlIgnore]
        public DateTime overrideLastWriteTime = DateTime.MinValue;

        public float rating = 1.0f;
        public bool challenge;          // Is this a challenge level?
        public string username;         // If this is a challenge, this the the username of the user that accepted the challenge.
        public int challengeID;         // User-specific challenge ID used when uploading response to challenge.  If this is 0
                                        // then it's just a regular uploaded level and not a challenge response.
        public string stuffFilename;    // Filename for file containing the "contents" of this level, ie all the objects and programming.
        public string preGame = "Nothing";          // Name of the PreGame (if any) to use.
        public string preGameLogo = "";
        public bool dirty = false;

        // Set of flags specifying a level's genres.
        public int genres = (int)Genres.None;

        // Tweak settings.
        public bool glassWalls = false;     // Setting this to true prevents most objects from falling off the edge of the world.
        public bool showCompass = true;     // Show the compass during run sim.
        public bool showResourceMeter = true;       // Show the resource meter during run sim.
        public bool enableResourceLimiting = true;  // Enable limiting of resources in run and edit modes.
        public float waveHeight = 0.2f;     // Nominal height of waves.
        public float waterStrength = 0.2f;  // Strength of water distortion effect, 0 (disabled) => 1 (very)
        public float cameraSpringStrength = 1.0f;
        public bool fixedCamera = false;    // Has the user specified a fixed camera location?
        public Vector3 fixedCameraFrom;     // View from position for fixed camera.
        public Vector3 fixedCameraAt;       // Look at position for fixed camera.
        public float fixedCameraDistance;
        public float fixedCameraPitch;
        public float fixedCameraRotation;
        public bool fixedOffsetCamera = false;
        public Vector3 fixedOffset;

        public Vector3 editCameraAt;        // Saved values for edit camera.
        public Vector3 editCameraFrom;
        public float editCameraRotation;
        public float editCameraPitch;
        public float editCameraDistance;

        public Vector3 playCameraAt;        // These values are only used during RunSim mode when the camera is no constrained to an actor or position.
        public Vector3 playCameraFrom;      // They are only saved when the user does an explicit save.
        public bool playCameraValid;        // Are the above values valid or should they be ignored.

        public float followCameraDistance;  // Only used when following a single actor.
        public bool followCameraValid;      // Is the above value valid?

        const int kNumGradientTaps = 5;
        public Vector4[] skyGradient = null;
        public int skyIndex = 0;

        public string lightRig = "Day";

        public float windMin = 0.0f;
        public float windMax = 0.5f;

        public bool debugPathFollow = false;
        public bool debugDisplayCollisions = false;
        public bool debugDisplayLinesOfPerception = false;
        public bool debugDisplayCurrentPage = false;

        public float uiVolume = 1.0f;
        public float foleyVolume = 1.0f;
        public float musicVolume = 1.0f;

        public bool startingCamera = false;
        public Vector3 startingCameraFrom = Vector3.Zero;
        public Vector3 startingCameraAt = Vector3.Zero;
        public float startingCameraRotation = 0f;
        public float startingCameraPitch = 0f;
        public float startingCameraDistance = 0f;

        public bool showVirtualController = false;

        public List<TouchGUIButtonXmlSetting> touchGuiButtonSettings = new List<TouchGUIButtonXmlSetting>();
        public List<ScoreXmlSetting> scoreSettings = new List<ScoreXmlSetting>();

        public XmlTerrainData xmlTerrainData;
        public XmlTerrainData2 xmlTerrainData2;

        public string waterNormalMapTextureFilename;
        public string envMapTextureFilename;

        [XmlElement(Type = typeof(Guid))]
        public List<Guid> creatableIds = new List<Guid>();

        public List<ChangeHistoryEntry> changeHistory = new List<ChangeHistoryEntry>();

        public string checksum = "";
        public DateTime lastSaveTime = DateTime.MinValue;//Used to determin if level is owned by user. 
                                                         //Note it should usually be the same as lastWriteTime but not the same as Modified.


        public List<Step> tutorialSteps = new List<Step>();

        #endregion

        #region Accessors
        /// <summary>
        /// Look-from position for fixed camera.
        /// </summary>
        [XmlIgnore]
        public Vector3 FixedCameraFrom
        {
            get { return fixedCameraFrom; }
            set { fixedCameraFrom = value; }
        }
        /// <summary>
        /// Look-at position for fixed camera.
        /// </summary>
        [XmlIgnore]
        public Vector3 FixedCameraAt
        {
            get { return fixedCameraAt; }
            set { fixedCameraAt = value; }
        }
        /// <summary>
        /// View distance for fixed camera.
        /// </summary>
        [XmlIgnore]
        public float FixedCameraDistance
        {
            get { return fixedCameraDistance; }
            set { fixedCameraDistance = value; }
        }
        /// <summary>
        /// Rotation for fixed camera.
        /// </summary>
        [XmlIgnore]
        public float FixedCameraRotation
        {
            get { return fixedCameraRotation; }
            set { fixedCameraRotation = value; }
        }
        /// <summary>
        /// Pitch for fixed camera.
        /// </summary>
        [XmlIgnore]
        public float FixedCameraPitch
        {
            get { return fixedCameraPitch; }
            set { fixedCameraPitch = value; }
        }

        /// <summary>
        /// Show the compass during run sim.
        /// </summary>
        [XmlIgnore]
        public bool ShowCompass
        {
            get { return showCompass; }
            set { showCompass = value; }
        }

        /// <summary>
        /// Show the resource meter during run sim.
        /// </summary>
        [XmlIgnore]
        public bool ShowResourceMeter
        {
            get { return showResourceMeter; }
            set { showResourceMeter = value; }
        }

        /// <summary>
        /// Enables resoruce limiting.
        /// </summary>
        [XmlIgnore]
        public bool EnableResourceLimiting
        {
            get { return enableResourceLimiting; }
            set { enableResourceLimiting = value; }
        }

        /// <summary>
        /// Array of colors (and positions) for the skybox gradient.
        /// This is OBSOLETE, use the index instead.
        /// </summary>
        [XmlIgnore]
        public Vector4[] SkyGradient
        {
            get { return skyGradient; }
            set { skyGradient = value; }
        }

        /// <summary>
        /// The index for which sky gradient to use.
        /// </summary>
        [XmlIgnore]
        public int SkyIndex
        {
            get { return skyIndex; }
            set { skyIndex = value; }
        }

        [XmlIgnore]
        public string Filename
        {
            get { return id.ToString() + ".Xml"; }
        }

        #endregion

        public XmlWorldData()
        {
        }

        public override void OnBeforeSave()
        {
#if !ADDIN
            BokuVersion = Program2.ThisVersion.ToString();
#endif
            KCodeVersion = Program2.CurrentKCodeVersion;

            // Since we're saving as the signed in person, use the current Auth info.
            creator = Auth.CreatorName;
            checksum = Auth.CreateChecksumHash(lastWriteTime);

            // Manipulate localized strings.
            BeforeSaveLocalizedString(ref name, ref originalName, ref localizedName, ref localizedNameDict);
            BeforeSaveLocalizedString(ref description, ref originalDescription, ref localizedDescription, ref localizedDescriptionDict);

            if (tutorialSteps != null)
            {
                foreach (Step step in tutorialSteps)
                {
                    BeforeSaveLocalizedString(ref step.GoalText, ref step.OriginalGoalText, ref step.LocalizedGoalText, ref step.LocalizedGoalTextDict);
                    BeforeSaveLocalizedString(ref step.GamepadText, ref step.OriginalGamepadText, ref step.LocalizedGamepadText, ref step.LocalizedGamepadTextDict);
                    BeforeSaveLocalizedString(ref step.MouseText, ref step.OriginalMouseText, ref step.LocalizedMouseText, ref step.LocalizedMouseTextDict);
                    BeforeSaveLocalizedString(ref step.TouchText, ref step.OriginalTouchText, ref step.LocalizedTouchText, ref step.LocalizedTouchTextDict);
                }
            }

            /*
            // Fake some data so we can see what it looks like in the Xml and do some testing.
            if (localizedNameDict == null)
            {
                localizedNameDict = new XmlSerializableDictionary<string, string>();
            }
            localizedNameDict.Add("EN", "English Title");
            localizedNameDict.Add("ES", "Spanish Title");
            */
        
        }   // end of OnBeforeSave()

        public override void OnAfterSave()
        {
            // For saving, we used the original versions of the strings but if this class
            // is still being used in memory we need to restore the localized versions.
            name = localizedName;
            description = localizedDescription;

            if (tutorialSteps != null)
            {
                foreach (Step step in tutorialSteps)
                {
                    step.GoalText = step.LocalizedGoalText;
                    step.GamepadText = step.LocalizedGamepadText;
                    step.MouseText = step.LocalizedMouseText;
                    step.TouchText = step.LocalizedTouchText;
                }
            }

        }   // end of OnAfterSave()

        protected override bool OnLoad()
        {
#if !ADDIN
            // Check to ensure the level wasn't written by a newer version of Kodu.
            int itVersion = int.Parse(this.KCodeVersion);
            if (itVersion > int.Parse(Program2.CurrentKCodeVersion))
                return false;
#endif
            FixUpNaNs();

            if (xmlTerrainData != null)
            {
                xmlTerrainData.FixUpTerrainTexturePaths();
                envMapTextureFilename = xmlTerrainData.envMapTextureFilename;
                waterNormalMapTextureFilename = xmlTerrainData.waterNormalMapTextureFilename;
            }
            /// Translate old sky gradients into an index
            if (skyGradient != null)
            {
#if !ADDIN
                skyIndex = SimWorld.SkyBox.Find(skyGradient);
                skyGradient = null;
#endif
            }

            // Assign guids to legacy worlds
            if (id == Guid.Empty)
                id = Guid.NewGuid();

            // Append stuff path to legacy worlds
            if (stuffFilename != null && stuffFilename != String.Empty && !stuffFilename.Contains(@"\"))
                stuffFilename = @"Xml\Levels\Stuff\" + stuffFilename;


            // Check for localizations.
            OnLoadLocalizedString(ref name, ref originalName, ref localizedName, ref localizedNameDict);
            OnLoadLocalizedString(ref description, ref originalDescription, ref localizedDescription, ref localizedDescriptionDict);

            if (tutorialSteps != null)
            {
                foreach (Step step in tutorialSteps)
                {
                    OnLoadLocalizedString(ref step.GoalText, ref step.OriginalGoalText, ref step.LocalizedGoalText, ref step.LocalizedGoalTextDict);
                    OnLoadLocalizedString(ref step.GamepadText, ref step.OriginalGamepadText, ref step.LocalizedGamepadText, ref step.LocalizedGamepadTextDict);
                    OnLoadLocalizedString(ref step.MouseText, ref step.OriginalMouseText, ref step.LocalizedMouseText, ref step.LocalizedMouseTextDict);
                    OnLoadLocalizedString(ref step.TouchText, ref step.OriginalTouchText, ref step.LocalizedTouchText, ref step.LocalizedTouchTextDict);
                }
            }

            return true;
        }   // end of OnLoad()

        /// <summary>
        /// We occasionally run across a file that as written with NaNs.  Find them
        /// and replace them with reasonable default values.
        /// </summary>
        private void FixUpNaNs()
        {
            // edit camera
            if (float.IsNaN(editCameraAt.X))
                editCameraAt = Vector3.Zero;
            if (float.IsNaN(editCameraFrom.X))
                editCameraFrom = Vector3.Zero;
            if (float.IsNaN(editCameraDistance))
                editCameraDistance = 0.0f;
            if (float.IsNaN(editCameraPitch))
                editCameraPitch = 0.0f;
            if (float.IsNaN(editCameraRotation))
                editCameraRotation = 0.0f;

            // fixed camera
            if (float.IsNaN(fixedCameraAt.X))
            {
                fixedCameraAt = Vector3.Zero;
                fixedCamera = false;
            }
            if (float.IsNaN(fixedCameraFrom.X))
            {
                fixedCameraFrom = Vector3.Zero;
                fixedCamera = false;
            }
            if (float.IsNaN(fixedCameraDistance))
            {
                fixedCameraDistance = 0.0f;
                fixedCamera = false;
            }
            if (float.IsNaN(fixedCameraPitch))
            {
                fixedCameraPitch = 0.0f;
                fixedCamera = false;
            }
            if (float.IsNaN(fixedCameraRotation))
            {
                fixedCameraRotation = 0.0f;
                fixedCamera = false;
            }

            // fixed offset camera
            if (float.IsNaN(fixedOffset.X))
            {
                fixedOffset = new Vector3(1, 1, 1);
                fixedOffsetCamera = false;
            }

            // follow camera
            if (float.IsNaN(followCameraDistance))
            {
                followCameraDistance = 6.0f;
                followCameraValid = false;
            }

            // play camera
            if (float.IsNaN(playCameraAt.X))
            {
                playCameraAt = Vector3.Zero;
                playCameraValid = false;
            }
            if (float.IsNaN(playCameraFrom.X))
            {
                playCameraFrom = Vector3.Zero;
                playCameraValid = false;
            }

            // starting camera
            if (float.IsNaN(startingCameraAt.X))
            {
                startingCameraAt = Vector3.Zero;
                startingCamera = false;
            }
            if (float.IsNaN(startingCameraFrom.X))
            {
                startingCameraFrom = Vector3.Zero;
                startingCamera = false;
            }
            if (float.IsNaN(startingCameraDistance))
            {
                startingCameraDistance = 0.0f;
                startingCamera = false;
            }
            if (float.IsNaN(startingCameraPitch))
            {
                startingCameraPitch = 0.0f;
                startingCamera = false;
            }
            if (float.IsNaN(startingCameraRotation))
            {
                startingCameraRotation = 0.0f;
                startingCamera = false;
            }

        }   // end of FixUpNaNs()


        public override void OnLoadFromFile(string filename)
        {
        }

        public override void OnBeforeSaveToFile()
        {
            if (this.overrideLastWriteTime != DateTime.MinValue)
            {
                this.lastWriteTime = this.overrideLastWriteTime;
                this.overrideLastWriteTime = DateTime.MinValue;
            }
            else
            {
#if !ADDIN
                // Don't refresh the date if running analytics.  This allows
                // the original date of the level to persist.
                if (!Program2.CmdLine.Exists("analytics"))
                {
                    this.lastWriteTime = DateTime.UtcNow;
                }
#endif
            }

        }   // end of OnBeforeSaveToFile()

        public string GetImageFilenameWithoutExtension()
        {
            try
            {
                if (Filename == null || Filename == String.Empty)
                    return id.ToString();
                Guid guid = new Guid(Path.GetFileNameWithoutExtension(Filename));
                return guid.ToString();
            }
            catch
            {
                return Path.GetFileNameWithoutExtension(Filename);
            }
        }   // end of GetImageFilenameWithoutExtension()

        //
        // Helper functions for working with localizable strings in the world data.
        //

        /// <summary>
        /// Before we save we need to check if the user has changed the string.
        /// If the user has changed the string then all the translations are invalid and should be removed.
        /// If the user didn't change the string then we need to restore the original string so it gets properly saved.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="originalStr"></param>
        /// <param name="localizedStr"></param>
        /// <param name="dict"></param>
        public static void BeforeSaveLocalizedString(ref string str, ref string originalStr, ref string localizedStr, ref XmlSerializableDictionary<string, string> dict)
        {
            // If the user has changed the strings, then keep the new strings and remove the localized versions.
            if (str != localizedStr)
            {
                // Reset everything to reflect the fact that we have a new string.
                dict = null;
                originalStr = str;
                localizedStr = str;
            }
            else
            {
                // No change so restore original so it's the one that gets saved out.
                str = originalStr;
            }
        }   // end of BeforeSaveLocalizedString()

        /// <summary>
        /// On load we want to see if there any localizations for the strings.  
        /// If there are then we look for one that matches the current language.  
        /// If not we just set all the strings to match each other.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="originalStr"></param>
        /// <param name="localizedStr"></param>
        /// <param name="dict"></param>
        public static void OnLoadLocalizedString(ref string str, ref string originalStr, ref string localizedStr, ref XmlSerializableDictionary<string, string> dict)
        {
            string curLang = Boku.Common.Localization.Localizer.LocalLanguage;

            originalStr = str;
            if (dict == null)
            {
                // If localized dictionary is null, must be an old level so do nothing.    
            }
            else
            {
                // If localized dictionary is not null, see if we have a string from current language to use.
                string locStr;
                if (dict.TryGetValue(curLang, out locStr))
                {
                    str = locStr;
                }
            }
            localizedStr = str;
        }   // end of OnLoadLocalizedString()

    }   // end of class XmlWorldData
}
