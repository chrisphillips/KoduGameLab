// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

// Uncomment this to allow AutoSave to write through to disk.  This is useful for debugging
// since it allows Resume to work from the main menu.
#define AutoSaveToDisk

//#define CAMERA_DEBUG
//#define DEEP_COPY_DEBUG

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;
using System.Windows.Forms;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Storage;


using Boku.Base;
using Boku.SimWorld;
using Boku.SimWorld.Path;
using Boku.SimWorld.Terra;
using Boku.Common;
using Boku.Common.HintSystem;
using Boku.Common.ParticleSystem;
using Boku.Common.TutorialSystem;
using Boku.Common.Xml;
using Boku.Common.Sharing;
using Boku.Programming;
using BokuShared;
using Boku.UI;
using Boku.UI2D;
using Boku.Input;
using Boku.Fx;

namespace Boku
{

    /// <summary>
    /// The load, save and reset functinality for InGame.
    /// </summary>
    public partial class InGame : GameObject, INeedsDeviceReset
    {
        #region Members

        static XmlWorldData xmlWorldData = null;    // Top level world info.
        static XmlLevelData xmlLevelData = null;    // "Stuff" ie bots, programs, etc.

        static string xmlWorldDataFullPath = null;
        static string xmlLevelDataFullPath = null;

        static bool autoSaved = false;              // If true, this means the level has been modified and autosaved
                                                    // but not yet saved for real.

        static int inreset = 0;
        #endregion

        #region Accessors

        /// <summary>
        /// If true, this means the level has been modified and autosaved
        /// but not yet saved for real.
        /// </summary>
        public static bool AutoSaved
        {
            get { return autoSaved; }
            set { autoSaved = value; }
        }

        /// <summary>
        /// Has the user made any edits?  An autosave will clear this.
        /// </summary>
        public static bool IsLevelDirty
        {
            get
            {
                return xmlWorldData == null
                  ? false
                  : xmlWorldData.dirty;
            }
            set
            {
                if (xmlWorldData != null)
                    xmlWorldData.dirty = value;
            }
        }

        /// <summary>
        /// Whether we're currently loading a level. Can be nested.
        /// </summary>
        public static bool InReset
        {
            get { return inreset > 0; }
            set
            {
                if (value)
                    ++inreset;
                else
                    --inreset;
                Debug.Assert(inreset >= 0);
            }
        }

        public static Guid CurrentWorldId
        {
            get
            {
                if (xmlWorldData != null)
                    return xmlWorldData.id;
                else
                    return Guid.Empty;
            }
        }
        public static string CurrentWorldName
        {
            get
            {
                if (xmlWorldData != null)
                    return xmlWorldData.name;
                else
                    return String.Empty;
            }
            set
            {
                if (xmlWorldData != null)
                    xmlWorldData.name = value;
                else
                    throw new InvalidOperationException("XmlWorldData is null");
            }
        }
        public static XmlWorldData XmlWorldData
        {
            get { return xmlWorldData; }
            set
            {
                Debug.Assert(value == null);
                xmlWorldData = null;
            }
        }

        #endregion



        /// <summary>
        /// Get called back by the SaveLevelDialog when the user makes a choice.
        /// </summary>
        /// <param name="dialog"></param>
        void OnSaveLevelDialogButton(SaveLevelDialog dialog)
        {
            if (dialog.Button == SaveLevelDialog.SaveLevelDialogButtons.Cancel)
            {
                //don't deactivate if the dialog was launched during a simulation
                if (CurrentUpdateMode != UpdateMode.RunSim)
                {
                    Deactivate();
                }

                if (saveLevelOnCancel != null)
                {
                    saveLevelOnCancel();
                }
                return;
            }

            if (dialog.Button == SaveLevelDialog.SaveLevelDialogButtons.Save)
            {
                // Done.  If this was caused by the SaveChanges dialog popping up then
                // we need to return to wherever the user was trying to go in the 
                // first place.  If this was caused by the user explicitely saving
                // then we should return to running.

                Deactivate();
                if (saveLevelOnComplete != null)
                {
                    saveLevelOnComplete();
                }
            }
        }   // end of OnSaveLevelDialogButton()

        #region Public

        public static string CurrentLevelFilename()
        {
            return BokuGame.Settings.MediaPath + BokuGame.MyWorldsPath + xmlWorldData.id.ToString() + @".Xml";
        }

        /// <summary>
        /// Saves the state of the current edit camera.
        /// </summary>
        public void SaveEditCamera()
        {
#if CAMERA_DEBUG
            Debug.Print("Saving edit camera : " + InGame.CurrentWorldName);
#endif

            xmlWorldData.editCameraFrom = Camera.From;
            xmlWorldData.editCameraAt = Camera.At;
            xmlWorldData.editCameraRotation = Camera.Rotation;
            xmlWorldData.editCameraPitch = Camera.Pitch;
            xmlWorldData.editCameraDistance = Camera.Distance;
        }   // end of SaveEditCamera()

        /// <summary>
        /// Restores the state of edit camera.
        /// Note: the way this is currently called, blend is true when switching from run to edit
        /// and false when loading a new level.
        /// </summary>
        /// <param name="blend">If true, causes a smooth, blended movement.  If false, just jumps.</param>
        public void RestoreEditCamera(bool blend)
        {
            CameraInfo.Mode = CameraInfo.Modes.Edit;

#if CAMERA_DEBUG
            Debug.Print("Restoring edit camera : " + InGame.CurrentWorldName + "  blend : " + blend.ToString());
#endif
            bool valid = xmlWorldData.editCameraFrom != xmlWorldData.editCameraAt;

            if (valid)
            {
                Camera.DesiredAt = xmlWorldData.editCameraAt;
                Camera.DesiredEyeOffset = xmlWorldData.editCameraFrom - xmlWorldData.editCameraAt;
                shared.CursorPosition = xmlWorldData.editCameraAt;
            }
            else
            {
                // Must have been a strange case.  Just pick a "reasonable" spot.
                Vector2 center = (Terrain.Min2D + Terrain.Max2D) / 2.0f;
                Vector3 position = new Vector3(center.X, center.Y, 0.0f);
                position.Z = Terrain.GetHeight(position);
                Camera.At = Camera.DesiredAt = position;
                Camera.EyeOffset = Camera.DesiredEyeOffset = new Vector3(-7.0f, -14.0f, 8.0f);
                shared.CursorPosition = Camera.At;
            }
        }   // end of RestoreEditCamera()

        /// <summary>
        /// Save the state of the current play mode camera.
        /// </summary>
        public void SavePlayModeCamera()
        {
#if CAMERA_DEBUG
            Debug.Print("Saving play camera : " + InGame.CurrentWorldName);
#endif

            // Save play mode camera settings if valid.
            if (Camera.PlayValid)
            {
                xmlWorldData.playCameraValid = true;
                xmlWorldData.playCameraAt = Camera.PlayCameraAt;
                xmlWorldData.playCameraFrom = Camera.PlayCameraFrom;
            }
            else
            {
                xmlWorldData.playCameraValid = false;
            }

            if (Camera.FollowCameraValid)
            {
                xmlWorldData.followCameraValid = true;
                xmlWorldData.followCameraDistance = Camera.FollowCameraDistance;
            }
            else
            {
                xmlWorldData.followCameraValid = false;
            }

        }   // end of SavePlayModeCamera()

        /// <summary>
        /// Restores the state of the play mode camera.
        /// </summary>
        public void RestorePlayModeCamera()
        {
#if CAMERA_DEBUG
            Debug.Print("Restoring play camera : " + InGame.CurrentWorldName);
#endif

            if (xmlWorldData.playCameraValid)
            {
                Camera.DesiredAt = XmlWorldData.playCameraAt;
                shared.CursorPosition = XmlWorldData.playCameraAt;
                Camera.DesiredEyeOffset = XmlWorldData.playCameraFrom - XmlWorldData.playCameraAt;
                Camera.EyeOffset = Camera.DesiredEyeOffset;
            }
            else
            {
#if CAMERA_DEBUG
                Debug.Print("  -- not really, play camera not valid, using current edit camera. : " + InGame.CurrentWorldName);
#endif
                // Invalid play camera, use edit camera.
                RestoreEditCamera(false);
            }

        }   // end of RestorePlayModeCamera()

        /// <summary>
        /// Saves the current camera state as the starting camera.
        /// </summary>
        public void SaveStartingCamera()
        {
#if CAMERA_DEBUG
            Debug.Print("Saving starting camera. : " + InGame.CurrentWorldName);
#endif

            StartingCamera = true;
            StartingCameraFrom = Camera.From;
            StartingCameraAt = Camera.At;
            StartingCameraRotation = Camera.Rotation;
            StartingCameraPitch = Camera.Pitch;
            StartingCameraDistance = Camera.Distance;
            IsLevelDirty = true;
        }   // end of SaveStartingCamera()

        /// <summary>
        /// Restores the starting camera state.
        /// </summary>
        public void RestoreStartingCamera()
        {
#if CAMERA_DEBUG
            Debug.Print("Restoring starting camera : " + InGame.CurrentWorldName);
#endif
            if (StartingCamera)
            {
                Camera.From = StartingCameraFrom;
                Camera.At = Camera.DesiredAt = StartingCameraAt;
                Camera.Rotation = Camera.DesiredRotation = StartingCameraRotation;
                Camera.Pitch = Camera.DesiredPitch = StartingCameraPitch;
                Camera.Distance = Camera.DesiredDistance = StartingCameraDistance;

                Camera.DesiredEyeOffset = Camera.From - Camera.At;

                shared.CursorPosition = Camera.At;
            }
            else
            {
#if CAMERA_DEBUG
                Debug.Print("  -- not really, starting camera not valid.  Doing nothing.");
#endif
            }
        }   // end of RestoreStartingCamera()

        public void SaveFixedCamera()
        {
#if CAMERA_DEBUG
            Debug.Print("Saving fixed camera : " + InGame.CurrentWorldName);
#endif

            Terrain.Current.FixedCamera = true;
            Terrain.Current.FixedCameraFrom = Camera.From;
            Terrain.Current.FixedCameraAt = Camera.At;
            Terrain.Current.FixedCameraDistance = Camera.Distance;
            Terrain.Current.FixedCameraRotation = Camera.Rotation;
            Terrain.Current.FixedCameraPitch = Camera.Pitch;
            IsLevelDirty = true;
        }   // end of SaveFixedCamera()

        public void RestoreFixedCamera()
        {
#if CAMERA_DEBUG
            Debug.Print("Restoring fixed camera : " + InGame.CurrentWorldName);
#endif

            if (Terrain.Current.FixedCamera)
            {
                shared.CursorPosition = Terrain.FixedCameraAt;
                Camera.DesiredAt = Terrain.FixedCameraAt;
                Camera.DesiredDistance = Terrain.FixedCameraDistance;
                Camera.DesiredRotation = Terrain.FixedCameraRotation;
                Camera.DesiredPitch = Terrain.FixedCameraPitch;
            }
            else
            {
#if CAMERA_DEBUG
                Debug.Print("  -- not really, fixed camera not valid.");
#endif
            }
        }   // RestoreFixedCamera()

        /// <summary>
        /// Saves the current edit changes to memory which can either be used as 
        /// a source for reseting or be written out to disk as a real save.
        /// </summary>
        public void AutoSaveLevel(string name)
        {
            //
            // Update xmlWorldData.
            //

            // Save the creatableIds
            xmlWorldData.creatableIds.Clear();
            Guid[] creatableIds = new Guid[creatables.Keys.Count];
            creatables.Keys.CopyTo(creatableIds, 0);
            foreach (Guid creatableId in creatableIds)
            {
                xmlWorldData.creatableIds.Add(creatableId);
            }

            // Touch GUI Button setting save.
            xmlWorldData.touchGuiButtonSettings.Clear();
            GUIButton[] buttons = GUIButtonManager.GetButtons();
            if (null != buttons)
            {
                for (int i = 0; i < buttons.Length; ++i)
                {
                    TouchGUIButtonXmlSetting setting = new TouchGUIButtonXmlSetting();

                    setting.color = GUIButtonManager.GetColorFromButtonIdx(i);
                    setting.label = buttons[i].Label;
                    // Only needed for back compat.  Otherwise the labels aren't rendered on older versions of Kodu.
                    setting.displayType = string.IsNullOrEmpty(buttons[i].Label) ? GUIButton.DisplayType.DT_Solid : GUIButton.DisplayType.DT_Labeled;

                    xmlWorldData.touchGuiButtonSettings.Add(setting);
                }
            }


            // Save score visibility settings
            xmlWorldData.scoreSettings.Clear();
            for (int i = (int)Classification.ColorInfo.First; i <= (int)Classification.ColorInfo.Last; ++i)
            {
                ScoreXmlSetting setting = new ScoreXmlSetting();

                setting.color = (Classification.Colors)i;

                Scoreboard.Score scoreObj = Scoreboard.GetScoreboardScore(setting.color);

                setting.visibility = scoreObj.Visibility;
                setting.persist = scoreObj.PersistFlag;
                setting.label = scoreObj.Labeled ? scoreObj.Label : "";

                xmlWorldData.scoreSettings.Add(setting);
            }

            //
            // xmlLevelData needs to be refreshed.
            //
            // TODO (****)  The whole interaction between autosave and linked levels needs
            //              to be rethought.  If anything is dirty it should be flagged and
            //              the user warned on run, not later when trying to switch levels.
            //              Even more useful would be to treat linked levels as a single
            //              unit which is autosaved so that you can edit the whole game, 
            //              freely changing levels, without being forced to save.
            //
            // If prevously autosaved we want to reload from disk since the InGame version
            // may have changed during run.  This case only comes up (I think) when running
            // a dirty level which switches levels.  On switch we need to saved the changes
            // we've made BUT we have to be sure to save the state as it was at the start 
            // of the run rather than the state as it exists in memory since characters may
            // have been added or destroyed.
            xmlLevelData = new XmlLevelData();
            if (AutoSaved && InGame.inGame.LinkingLevels)
            {
                // Get rid of whatever objects we currently have in memory.
                DeactivateAllGameThings();
                Refresh(BokuGame.gameListManager.updateList, BokuGame.gameListManager.renderList);

                // Load fresh from Autosave.
                xmlLevelData = new XmlLevelData();
                xmlLevelData.ReadFromXml(xmlWorldData, AddThing);

                // When the actors are reloaded their state is inactive.  We need
                // to put them into paused and call refresh so that they get added
                // to the correct update and render lists.
                foreach (GameThing thing in InGame.inGame.gameThingList)
                {
                    thing.PendingState = GameThing.State.Paused;
                }
                Refresh(BokuGame.gameListManager.updateList, BokuGame.gameListManager.renderList);
            }
            else
            {
                xmlLevelData.FromGame(InGame.inGame.gameThingList);
            }

            // And now the terrain.  Note that this call will first check if the 
            // terrain has been modified.  If so, then it will create/save the
            // new file and then modify the xmlWorldData to reference the newly
            // created filename.
            CreateNewTerrainFiles(xmlWorldData, name, false);

#if AutoSaveToDisk
            {
                // Get the new filenames.
                string worldFilename = BokuGame.MyWorldsPath + name + @".Xml";
                string stuffFilename = BokuGame.MyWorldsStuffPath + name + @".Xml";

                // Update XmlWorldData with new stuff name.
                xmlWorldData.stuffFilename = stuffFilename;

                // Save the stuff file.
                xmlLevelData.Save(BokuGame.Settings.MediaPath + stuffFilename, XnaStorageHelper.Instance);

                // The terrain has already been saved above so we
                // don't need to do anything more with it here.

                // We also don't need an autosaved thumbnail.

                // Save the world file.
                InGame.XmlWorldData.Save(BokuGame.Settings.MediaPath + worldFilename, XnaStorageHelper.Instance);
            }

            /// Don't flush file changes to storage hardware for just an autosave. 
            /// We'll continue flushing on internal build, so resume will still
            /// work after breaking in the debugger etc. ***.
#endif

            IsLevelDirty = false;
            AutoSaved = true;

        }   // end of AutoSaveLevel()



        /// <summary>
        /// Saves the level to disk.
        /// </summary>
        /// <param name="newName">The user has changed the name of the level.</param>
        public void SaveLevel(bool newName, bool preserveLinks)
        {
            // Get a version of the old level metadata into memory for comparison.  We use this
            // to see if any of the level links have been changed.
            LevelMetadata oldLevelData = null;

            if (XmlDataHelper.CheckWorldExistsByGenre(xmlWorldData.id, (Genres)xmlWorldData.genres))
            {
                oldLevelData = XmlDataHelper.LoadMetadataByGenre(xmlWorldData.id, (Genres)xmlWorldData.genres);
            }

            // If the name has changed, force an autosave so that if
            // we next resume, edit, then save we get the correct name
            // pre-loaded into the "new level name" text dialog.
            if (newName)
            {
                UnDoStack.OverwriteTopOfStack();

                // If a new name, at the very least we need a new guid.
                xmlWorldData.id = Guid.NewGuid();
#if DEEP_COPY_DEBUG
                Debug.Print("new guid : " + xmlWorldData.id.ToString());
#endif
            }

            // If we're saving the EmptyWorld we should generate a new guid.
            if (MiniHub.Instance.newWorldDialog.IsNewWorld(xmlWorldData.id.ToString()))
            {
                xmlWorldData.id = Guid.NewGuid();
#if DEEP_COPY_DEBUG
                Debug.Print("new guid : " + xmlWorldData.id.ToString());
#endif
            }

            // If genres hasn't been populated yet, populate it with MyWorlds flag.
            if (xmlWorldData.genres == 0)
            {
                xmlWorldData.genres |= (int)Genres.MyWorlds;
            }

            // We're saving a non My Worlds world, deep copy this world and all linked worlds.
            if ((xmlWorldData.genres & (int)Genres.BuiltInWorlds) != 0 ||
                (xmlWorldData.genres & (int)Genres.Downloads) != 0)
            {
                xmlWorldData.id = Guid.NewGuid();
                xmlWorldData.genres = (int)Genres.MyWorlds;

                // Copy forward
                DeepCopyNonLocalLink(xmlWorldData, forwards: true);
                // Copy backward
                DeepCopyNonLocalLink(xmlWorldData, forwards: false);
            }
            else
            {
                //if the user chose not to preserve links, zero them out, otherwise, update them (deep copy if necessary
                if (!preserveLinks)
                {
                    xmlWorldData.LinkedToLevel = null;
                    xmlWorldData.LinkedFromLevel = null;
                }
                else
                {
                    //if we used to be linked to a level, check if it is different than the new linked to level (if we have one),
                    //and if so, then clear out the old level's backwards link
                    if (oldLevelData != null && oldLevelData.LinkedToLevel != null && xmlWorldData.LinkedToLevel != oldLevelData.LinkedToLevel)
                    {
                        //load the next world and update it's link from (we can guarantee it's local - otherwise we would have taken the other branch
                        LevelMetadata oldLinkedToWorld = oldLevelData.NextLink();

                        if (oldLinkedToWorld != null)
                        {
                            //clean up the linked from info on the target level
                            oldLinkedToWorld.LinkedFromLevel = null;

                            //re-save the old linked to world
                            XmlDataHelper.UpdateWorldMetadata(oldLinkedToWorld);
                        }
                    }


                    //the world being saved was already local - check to make sure the links are still consistent
                    if (xmlWorldData.LinkedToLevel != null)
                    {
                        //did the player link to a non-local level?  deep copy it locally if so
                        if (!XmlDataHelper.CheckWorldExistsByGenre((Guid)xmlWorldData.LinkedToLevel, Genres.MyWorlds))
                        {
                            if (XmlDataHelper.CheckWorldExistsByGenre((Guid)xmlWorldData.LinkedToLevel, Genres.Downloads) ||
                                XmlDataHelper.CheckWorldExistsByGenre((Guid)xmlWorldData.LinkedToLevel, Genres.BuiltInWorlds))
                            {
                                //not a local level, need perform a deep copy and link to that (only need to copy forward, user can't modify previous level directly)                            
                                DeepCopyNonLocalLink(xmlWorldData, true);
                            }
                            else
                            {
                                //clean out broken links when saving
                                xmlWorldData.LinkedToLevel = null;
                            }
                        }
                        else
                        {
                            //load the next world and update it's link from (we can guarantee it's local - otherwise we would have taken the other branch
                            LevelMetadata nextWorld = XmlDataHelper.LoadMetadataByGenre((Guid)xmlWorldData.LinkedToLevel, Genres.MyWorlds);

                            //check if there's an old link that needs dissolving - player was already warned 
                            //when they set this link, so no dialog needed here
                            if (nextWorld.LinkedFromLevel != null && nextWorld.LinkedFromLevel != xmlWorldData.id)
                            {
                                //we need to clear the link from the old world to keep things consistent
                                if (XmlDataHelper.CheckWorldExistsByGenre((Guid)nextWorld.LinkedFromLevel, Genres.MyWorlds))
                                {
                                    LevelMetadata oldLink = XmlDataHelper.LoadMetadataByGenre((Guid)nextWorld.LinkedFromLevel, Genres.MyWorlds);
                                    oldLink.LinkedToLevel = null;

                                    //re-save the old link info
                                    XmlDataHelper.UpdateWorldMetadata(oldLink);
                                }
                            }

                            //update the target level's backwards link
                            nextWorld.LinkedFromLevel = xmlWorldData.id;

                            //save out the target level with the new link info
                            XmlDataHelper.UpdateWorldMetadata(nextWorld);
                        }
                    }

                    //clean up broken from links
                    if (xmlWorldData.LinkedFromLevel != null && !XmlDataHelper.CheckWorldExistsByGenre((Guid)xmlWorldData.LinkedFromLevel, Genres.MyWorlds))
                    {
                        xmlWorldData.LinkedFromLevel = null;
                    }

                    //also, if saving as a new name and preserving the links, update the level that points to us to point to the latest version
                    if (newName)
                    {
                        //did we have a level linking to us?  if so, it needs to be updated to the new version
                        if (xmlWorldData.LinkedFromLevel != null && XmlDataHelper.CheckWorldExistsByGenre((Guid)xmlWorldData.LinkedFromLevel, Genres.MyWorlds))
                        {
                            //load the previous world, check if it had a linked to flag
                            LevelMetadata prevWorld = XmlDataHelper.LoadMetadataByGenre((Guid)xmlWorldData.LinkedFromLevel, Genres.MyWorlds);

                            //make sure the previous world's link is consistent as well
                            if (prevWorld.LinkedToLevel != null &&
                                prevWorld.LinkedToLevel != xmlWorldData.id &&
                                XmlDataHelper.CheckWorldExistsByGenre((Guid)prevWorld.LinkedToLevel, Genres.MyWorlds))
                            {
                                //also clear out the old links
                                LevelMetadata oldVersion = XmlDataHelper.LoadMetadataByGenre((Guid)prevWorld.LinkedToLevel, Genres.MyWorlds);
                                oldVersion.LinkedFromLevel = null;
                                oldVersion.LinkedToLevel = null;

                                //re-save the old link info
                                XmlDataHelper.UpdateWorldMetadata(oldVersion);

                                //already linked from another level to this level - absolve the link...
                                Debug.WriteLine("WARNING: Target level already has a link from a level (" + prevWorld.WorldId + ") that doesn't know about us - still updating it's linked to for consistency!");
                            }
                            prevWorld.LinkedToLevel = xmlWorldData.id;
                            XmlDataHelper.UpdateWorldMetadata(prevWorld);
                        }
                    }
                }
            }

            //at this point, all links should be updated, deep copies performed if necessary - all that is left is to finish saving the current level
            SaveLevel(xmlWorldData, xmlLevelData, ThumbNail, newName);

            IsLevelDirty = false;
            AutoSaved = false;

        }   // end of InGame SaveLevel()


        /// <summary>
        /// Saves the level to disk.
        /// </summary>
        /// <param name="newName">The user has changed the name of the level.</param>
        void SaveLevel(XmlWorldData worldData, XmlLevelData levelData, Texture2D thumbnail, bool newName)
        {
            // If we have a new name then we need a new id.  Also if the level
            // we're saving is a BuiltInWorld, give it a new ID so we don't
            // overwrite the built in one.
            worldData.creator = Auth.CreatorName;

            // NEVER save the logo since we don't want people using it for their own levels.
            worldData.preGameLogo = "";

            // If we're saving with a different gamertag than is currently
            // at the end of the change history list, add a new entry.
            if (worldData.changeHistory.Count == 0 || worldData.creator != worldData.changeHistory[worldData.changeHistory.Count - 1].gamertag)
            {
                ChangeHistoryEntry entry = new ChangeHistoryEntry();
                entry.gamertag = worldData.creator;
                entry.time = DateTime.Now;

                worldData.changeHistory.Add(entry);
            }
            else
            {
                // Just refresh the last-touched time.
                worldData.changeHistory[worldData.changeHistory.Count - 1].time = DateTime.Now;
            }

            // Get the new filenames.
            string worldFilename = BokuGame.MyWorldsPath + worldData.id.ToString() + @".Xml";
            string stuffFilename = BokuGame.MyWorldsStuffPath + worldData.id.ToString() + @".Xml";

            // Update XmlWorldData with new stuff name.
            worldData.stuffFilename = stuffFilename;

            // Save the stuff file.
            levelData.Save(BokuGame.Settings.MediaPath + stuffFilename, XnaStorageHelper.Instance);

            // Save the terrain.  Note, may change XmlWorldData
            // so this needs to be saved before then.
            // We used to allow the ability to share terrain files among levels but this
            // was causing error in setup where multiple users are redirected to the same folder.
            // So now we always save the terrain file and the name always matches the world's.
            {
                // Force CreateNewTerrainFiles() to do a save even if it doesn't think it's needed.
                CreateNewTerrainFiles(worldData, worldData.id.ToString(), true);
            }

            // Save the thumbnail.
            string thumbFilename = BokuGame.Settings.MediaPath + BokuGame.MyWorldsPath + worldData.id.ToString() + ".jpg";
            if (thumbnail != null)
            {
                Storage4.TextureSaveAsJpeg(thumbnail, thumbFilename);
            }

            // Save large size 800x600 image.
            try
            {
                GraphicsDevice device = BokuGame.bokuGame.GraphicsDevice;
                SpriteBatch batch = UI2D.Shared.SpriteBatch;

                // Crop and size full render target to 800x600 rt.
                float aspectFull = InGame.inGame.FullRenderTarget0.Width / (float)InGame.inGame.FullRenderTarget0.Height;
                float aspectLarge = 800.0f / 600.0f;

                InGame.SetRenderTarget(InGame.inGame.LargeRenderTarget);
                Rectangle dst = new Rectangle(0, 0, 800, 600);

                batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
                // If we have a wider aspect ratio, crop the left/right sides.
                if (aspectFull > aspectLarge)
                {
                    int srcWidth = (int)(InGame.inGame.FullRenderTarget0.Width / aspectFull * aspectLarge);
                    int margin = (int)((InGame.inGame.FullRenderTarget0.Width - srcWidth) / 2.0f);
                    Rectangle src = new Rectangle(margin, 0, srcWidth, InGame.inGame.FullRenderTarget0.Height);
                    batch.Draw(InGame.inGame.FullRenderTarget0, dst, src, Color.White);
                }
                else
                {
                    // If we have a tall aspect ratio, crop the top/bottom edges.
                    int srcHeight = (int)(InGame.inGame.FullRenderTarget0.Height / aspectLarge * aspectFull);
                    int margin = (int)((InGame.inGame.FullRenderTarget0.Height - srcHeight) / 2.0f);
                    Rectangle src = new Rectangle(0, margin, InGame.inGame.FullRenderTarget0.Width, srcHeight);
                    batch.Draw(InGame.inGame.FullRenderTarget0, dst, src, Color.White);
                }
                batch.End();
                InGame.SetRenderTarget(null);

                string filename = BokuGame.Settings.MediaPath + BokuGame.MyWorldsPath + worldData.id.ToString() + "_800.jpg";
                if (InGame.inGame.LargeRenderTarget != null)
                {
                    Storage4.TextureSaveAsJpeg(InGame.inGame.LargeRenderTarget, filename);
                }
            }
            catch (Exception e)
            {
                if (e != null)
                {
                }
            }


            // Save the world file.
            worldData.Save(BokuGame.Settings.MediaPath + worldFilename, XnaStorageHelper.Instance);

        }   // end of InGame SaveLevel()

        /// <summary>
        /// Does a deep copy of a non-local world (non-MyWorlds, so it should be in Downloads) 
        /// and all it linked levels.
        /// This requires that all the levels get a new guid.
        /// </summary>
        /// <param name="linkedWorld">This is the world we're coming from.</param>
        /// <param name="forwards">Are we going forwards in the chain or backwards?</param>
        /// <returns>true on success, false on error.</returns>
        bool DeepCopyNonLocalLink(XmlWorldData linkedWorld, bool forwards)
        {
#if DEEP_COPY_DEBUG
            Debug.Print("Start DeepCopy, forwards : " + forwards.ToString());
            Debug.Print("linkedWorld is " + linkedWorld.id);
#endif

            // Get the link to the next world.
            Guid? currentGuid = null;

            if (forwards)
            {
                currentGuid = linkedWorld.LinkedToLevel;
            }
            else
            {
                currentGuid = linkedWorld.LinkedFromLevel;
            }

            // No link? easy.
            if (currentGuid == null)
            {
#if DEEP_COPY_DEBUG
                Debug.Print("No Link");
#endif
                return true;
            }
            
#if DEEP_COPY_DEBUG
            Debug.Print("Current (old) Guid : " + currentGuid.ToString());
#endif

            string worldFilename = Path.Combine(Storage4.UserLocation, "Content", BokuGame.DownloadsPath, currentGuid.ToString() + ".Xml");
            XmlWorldData currentWorld = XmlWorldData.Load(worldFilename, XnaStorageHelper.Instance);
            if (currentWorld == null)
            {
                return false;
            }

            // Create a new guid for the current level.
            Guid newGuid = Guid.NewGuid();
#if DEEP_COPY_DEBUG
            Debug.Print("New Guid : " + newGuid.ToString());
#endif

            // Fix up links between linkedWorld and currentWorld.
            // Note: linkedWorld is written back out to disk by the calling function.
            // This should be the only touch needed to the original file.
            if (forwards)
            {
                linkedWorld.LinkedToLevel = newGuid;
                currentWorld.LinkedFromLevel = linkedWorld.id;
            }
            else
            {
                linkedWorld.LinkedFromLevel = newGuid;
                currentWorld.LinkedToLevel = linkedWorld.id;
            }

            // Update id.
            currentWorld.id = newGuid;

            // Update tags.
            currentWorld.genres &= ~(int)Genres.Downloads;  // Clear Downloads.
            currentWorld.genres |= (int)Genres.MyWorlds;    // Set MyWorlds.

            // Stuff file.
            {
                string srcName = Path.Combine(Storage4.UserLocation, "Content", BokuGame.DownloadsStuffPath, currentGuid.ToString() + ".Xml");
                string dstName = Path.Combine(Storage4.UserLocation, "Content", BokuGame.MyWorldsStuffPath, newGuid.ToString() + ".Xml");
                File.Copy(srcName, dstName);
#if DEEP_COPY_DEBUG
                Debug.Print("Copying " + srcName + " to " + dstName);
#endif
            }

            // Terrain file.
            {
                string srcName = Path.Combine(Storage4.UserLocation, "Content", BokuGame.TerrainPath, currentGuid.ToString() + ".Map");
                string dstName = Path.Combine(Storage4.UserLocation, "Content", BokuGame.TerrainPath, newGuid.ToString() + ".Map");
                File.Copy(srcName, dstName);
#if DEEP_COPY_DEBUG
                Debug.Print("Copying " + srcName + " to " + dstName);
#endif
            }

            // Thumbnail & screenshots.
            {
                string srcFolder = Path.Combine(Storage4.UserLocation, "Content", BokuGame.DownloadsPath);
                string[] files = Storage4.GetFiles(srcFolder, StorageSource.UserSpace);

                // Loop over files in Downloads folder.
                string currentGuidString = currentGuid.ToString();
                string newGuidString = newGuid.ToString();
                foreach (string filename in files)
                {
                    // Look for images with matching guid.
                    if (filename.Contains(currentGuidString))
                    {
                        // Look for image files.
                        string ext = Path.GetExtension(filename);
                        ext = ext.ToLower();
                        if (ext == ".jpg" || ext == ".dds")
                        {
                            // Change path from Downloads to MyWorlds.
                            string dstName = filename.Replace("Downloads", "MyWorlds");
                            // Change to new guid.
                            dstName = dstName.Replace(currentGuidString, newGuidString);
                            File.Copy(filename, dstName);
#if DEEP_COPY_DEBUG
                            Debug.Print("Copying " + filename + " to " + dstName);
#endif
                        }
                    }
                }
            }

            // Recurse.
            DeepCopyNonLocalLink(currentWorld, forwards);

            // Write changes to current file.  Note that we do this AFTER recursing
            // since the call may change the links.
            worldFilename = Path.Combine("Content", BokuGame.MyWorldsPath, newGuid.ToString() + ".Xml");
            currentWorld.Save(worldFilename, XnaStorageHelper.Instance);
#if DEEP_COPY_DEBUG
            Debug.Print("Writing current file to : " + worldFilename);
            Debug.Print("  name : ", currentWorld.name);
            Debug.Print("Done with DeepCopy");
#endif

            return true;
        }   // end of DeepCopyNonLocalLink()

        /// <summary>
        /// This checks if the terrain heightmap has been modified.  If it has then a 
        /// new file is written using the given fileroot and then the XmlWorldData 
        /// structure is updated to reflect the new filename.
        /// </summary>
        /// <param name="forceSave">Set to true to force the saving of the terrain file even if the modified flag is false.</param>
        public void CreateNewTerrainFiles(XmlWorldData xmlWorldData, string fileroot, bool forceSave)
        {
            if (shared.heightMapModified || forceSave)
            {
                // Create new name for height map file.
                string fullPath = BokuGame.TerrainPath + fileroot + ".Map";

                // Save the file.
                Terrain.SaveHeight(BokuGame.Settings.MediaPath + fullPath);

                // Update the name in the structure.
                xmlWorldData.xmlTerrainData2.virtualMapFile = fullPath;

                // Reset flags.
                shared.heightMapModified = false;
            }

        }   // end of InGame CreateNewWorldFiles()

        /// <summary>
        /// Prepend the full path and postfix the extention, then load the auto save file.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>true on success</returns>
        public bool LoadAutoSave(string name, bool andRun, bool initUndoStack)
        {
            string fullPath = BokuGame.Settings.MediaPath + BokuGame.MyWorldsPath + name + @".Xml";

            bool result;
            // SGI_MOD - using the normal load level and run to fix the pregame being active on the first autosave load
            result = LoadLevelAndRun(fullPath, keepPersistentScores: false, newWorld: false, andRun: andRun, initUndoStack: initUndoStack);

            /// If we've just loaded an autosave, it's because we did an undo/redo.
            /// That means we're editing and are still probably different than the
            /// original. We still need a save, which is what AutoSaved really means.
            AutoSaved = IsLevelDirty;
            IsLevelDirty = false;

            return result;
        }   // end of LoadAutoSave()

        /// <summary>
        /// Load the given level.  Despite the name, the 'AndRun' part is controlled by a parameter.
        /// TODO(****) Should clean up this naming.
        /// </summary>
        /// <param name="levelFullPath"></param>
        /// <param name="keepPersistentScores">Used when loading linked levels to preserve persistent scores.</param>
        /// <param name="newWorld">If New World was chosen from either Main Menu or Home Menu.</param>
        /// <param name="andRun">If true, we're loading a world to be run.  If false, loading for edit.</param>
        /// <param name="initUndoStack">Should the Undo stack be initialized?  Normally true but false when loading due to an undo command.</param>
        /// <returns>True on successful loading, false otherwise.</returns>
        public bool LoadLevelAndRun(string levelFullPath, bool keepPersistentScores, bool newWorld, bool andRun = true, bool initUndoStack = true)
        {
            // These will get reloaded for this level.  This clears 
            // up any NamedFilters from the previous level.
            NamedFilter.UnregisterAllNamedFiltersInCardSpace();

            if (!LoadLevel(levelFullPath, keepPersistentScores: keepPersistentScores, newWorld: newWorld, andRun: andRun))
            {
                return false;
            }

            IsLevelDirty = false;
            AutoSaved = false;

            if (initUndoStack)
            {
                UnDoStack.Init();
            }

            if (andRun)
            {
                InGame.ApplyInlining();
            }

            // Flush creatables from list AFTER autosave happens, otherwise they will be lost forever (corrupted gamestate).
            // This also must come after ApplyInlining, otherwise creatables don't get inlined code.
            Refresh(BokuGame.gameListManager.updateList, BokuGame.gameListManager.renderList);

            // Activate the PreGame, if any.
            if (InGame.inGame.PreGame != null && andRun)
            {
                InGame.inGame.PreGame.Active = true;
            }

            // Activate Tutorial, if any, but only if running.
            if (InGame.XmlWorldData.tutorialSteps.Count > 0 && andRun)
            {
                TutorialManager.Activate();
            }
            else
            {
                // If not activating, then kill off anything currently running.
                TutorialManager.Deactivate();
            }

            return true;
        }   // end of LoadLevelAndRun()

        /// <summary>
        /// Resets the simulation.  Will reload data if needed.  Requires the 
        /// current InGame.inGame.xmlWorldData to be up to date.
        /// </summary>
        /// <param name="preserveScores">When true, this causes the score values not to be reset.  This is only used by the DO Reset-World verb since there is a seperate Reset-Scores tile.</param>
        /// <param name="removeCreatablesFromScene"></param>
        /// <param name="keepPersistentScores">This is true when the ResetSim call is for loading a linked level during run.</param>
        public void ResetSim(bool preserveScores, bool removeCreatablesFromScene, bool keepPersistentScores)
        {
            // Since preserveScores happens while executing a Reset action and keepPersistentScores 
            // happens while linking levels we should never see both true at the same time.
            Debug.Assert(!(preserveScores && keepPersistentScores), "Both should never be true.");

            InReset = true;
            LimitBudget = false;

            // 
            // Reset InGame state that shouldn't survive loading or reloading.
            //

            // Kill all active twitches.  This is here in particular to ensure that
            // any color changes that happened while in-game have a change to be 
            // finalized before edit mode is reloaded.  This prevents the wrong
            // color from being shown in edit mode.
            TwitchManager.KillAllTwitches();

            // Unload all brains so that death sensors won't be triggered.
            UnregisterAllBrains();

            VictoryOverlay.Reset();

            HealthBarManager.UnregisterAllActors();

            UnregisterAllCreatables();

            // Clear our cache of creatables since we're reloading them.
            ClearCreatables();

            // Kill any outstanding audio
            BokuGame.Audio.StopAllAudio();

            // Always start in edit mode, if there's a user controlled
            // actor we'll switch camera modes on the fly.
            //CameraInfo.Mode = CameraInfo.Modes.Edit;
            //CameraInfo.FirstPerson = false;

            // Remove any active particle emitters.  But, restore the dust 
            // emitter used while dragging things around in edit mode.
            shared.dustEmitter.RemoveFromManager();
            ExplosionManager.Suspend();
            ParticleSystemManager.ClearAllEmitters();
            ExplosionManager.Resume();
            shared.dustEmitter.AddToManager();

            // Before deactivating any gamethings, we must refresh the list to activate any pending active objects.
            // Not doing this may cause a currently inactive but pending active object to become both pending and
            // currently inactive, which is not a valid state for objects in this list.
            Refresh(BokuGame.gameListManager.updateList, BokuGame.gameListManager.renderList);
            // Deactivate all the existing level things since we're reloading.
            DeactivateAllGameThings();
            // Force a Refresh so they're also removed.
            Refresh(BokuGame.gameListManager.updateList, BokuGame.gameListManager.renderList);

            // Ensure we're not paused unless in an active preGame.
            if (!(InGame.inGame.PreGame != null && InGame.inGame.PreGame.Active))
            {
                Time.Paused = false;
                Time.ClockRatio = 1.0f;
            }

            // Load the terrain, but only if we need to.
            // TODO (****) Does this EVER not blow away the terrain?
            if (terrain == null || terrain.XmlWorldData != xmlWorldData)
            {
                // Blow away and garbage-collect the old terrain
                DiscardTerrain();

                terrain = new Terrain(xmlWorldData);
            }

            // Enable any pre-game objects for this level.
            //Note: pre-game actually relies on the cached version of xml data in terrain (why is a good question...)
            //Don't set up pre game until we're sure the correct xml world data is in terrain
            SetUpPreGame();

            // See if we have a new env map.
            InitFromWorldData(xmlWorldData);

            // Reset scores except for persistent ones when we're going to a linked level.
            if (!preserveScores)
            {
                if (keepPersistentScores)
                {
                    // Only reset the non-persistent scores.
                    Scoreboard.Reset(ScoreResetFlags.AllSkipPersistent);
                }
                else
                {
                    Scoreboard.Reset(ScoreResetFlags.All);
                }
            }

            GUIButtonManager.ClearAllButtonState();
            foreach (TouchGUIButtonXmlSetting setting in xmlWorldData.touchGuiButtonSettings)
            {
                GUIButton button = GUIButtonManager.GetButton(setting.color);
                Debug.Assert(null != button);

                button.Label = setting.label;
            }

            foreach (ScoreXmlSetting setting in xmlWorldData.scoreSettings)
            {
                Scoreboard.Score scoreObj = Scoreboard.GetScoreboardScore(setting.color);
                Debug.Assert(null != scoreObj);

                scoreObj.Visibility = setting.visibility;
                scoreObj.PersistFlag = setting.persist;

                scoreObj.Label = setting.label;
                scoreObj.Labeled = (null != setting.label) && (setting.label.Length > 0); //Labeled if label exists.
            }

            // Register placeholder creatables.  This is necessary because they
            // must exist before loading the stuff XML or else they'll be filtered
            // out of the reflexes while loading.  After load, we re-register with
            // the actual creatables.
            foreach (Guid creatableId in xmlWorldData.creatableIds)
            {
                RegisterPlaceholderCardSpace(creatableId);
            }

            bool success = true;
            ResetActorCost();
            if (xmlLevelDataFullPath != xmlWorldData.stuffFilename)
            {
                // Now load the contents (objects and their programming).
                xmlLevelData = new XmlLevelData();
                success = xmlLevelData.ReadFromXml(xmlWorldData, AddThing);
                xmlLevelDataFullPath = success ? xmlWorldData.stuffFilename : null;
            }
            else
            {
                // The stuff file isn't different so just reload 
                // the objects from memory instead of going back to disk.
                xmlLevelData.ToGame(AddThing);
            }

            // Unregister placeholder creatables. Below, we re-register with the actual creatables.
            foreach (Guid creatableId in xmlWorldData.creatableIds)
            {
                UnregisterCardSpace(creatableId);
            }

            if (success)
            {
                CameraInfo.CameraFocusGameActor = null;

                // Activate all the new game things we just read in.
                for (int i = 0; i < gameThingList.Count; i++)
                {
                    GameThing thing = gameThingList[i];

                    thing.Activate();

                    GameActor actor = thing as GameActor;

                    if (actor != null)
                    {
                        // Register creatables.
                        if (actor.Creatable)
                        {
                            RegisterCreatable(actor);
                        }

                        // Reset.
                        actor.Reset();
                    }

                    // Set to preferred height relative to the ground.
                    thing.SetAltitude();

                }   // end for each thing in the gameThingList

                // Force a Refresh so the newly activated objects will get added to the 
                // render and update lists.  This prevents a 1-frame flicker on reset.
                Refresh(BokuGame.gameListManager.updateList, BokuGame.gameListManager.renderList);

                // Added this block so that creatables will be removed when the reset verb fires.
                if (removeCreatablesFromScene)
                {
                    // Now that creatables have been activated, we can deactivate them.
                    RemoveCreatablesFromScene();
                }

                InGame.inGame.MakeObjectBounds();
            }

            SkyBox.Setup(InGame.TotalBounds);

            cursorClone = null;

            SensorTargetSpares.Clear();

            CameraInfo.ResetAllLists();

            // On reset, the default is to assume we're going into run mode
            // and we need to set the starting camera.  If either of these 
            // aren't true, we can override it later.  Note, this call is
            // probably not needed?
            RestoreStartingCamera();

            // Check user programming for mouse usage.
            bool usesMouseLeft = false;
            bool usesMouseRight = false;
            bool usesMouseHover = false;
            InGame.inGame.ProgramUsesMouseInput = BaseHint.CheckMouseUsage(out usesMouseLeft, out usesMouseRight, out usesMouseHover);
            InGame.inGame.ProgramUsesLeftMouse = usesMouseLeft;
            InGame.inGame.ProgramUsesRightMouse = usesMouseRight;
            InGame.inGame.ProgramUsesMouseHover = usesMouseHover;

            // Help prevent leakage of UI inputs into gameplay.
            GamePadInput.IgnoreUntilReleased(Buttons.A);
            GamePadInput.IgnoreUntilReleased(Buttons.B);
            GamePadInput.IgnoreUntilReleased(Buttons.X);
            GamePadInput.IgnoreUntilReleased(Buttons.Y);

            LimitBudget = true;
            InReset = false;
        }   // end of InGame ResetSim()

        public void DiscardTerrain()
        {
            if (terrain != null)
            {
                BokuGame.Unload(terrain);
                terrain.Dispose();
                terrain = null;
            }
            GC.Collect();
        }

        #endregion

        #region Internal

        /// <summary>
        /// Once xmlWorldData is loaded, initialize everything dependent on it.
        /// </summary>
        /// <param name="xmlWorldData"></param>
        void InitFromWorldData(XmlWorldData xmlWorldData)
        {
            BokuGame.bokuGame.shaderGlobals.EnvTextureName = xmlWorldData.envMapTextureFilename;
            BokuGame.bokuGame.shaderGlobals.SetLightRig(xmlWorldData.lightRig);
            Terrain.SkyIndex = xmlWorldData.SkyIndex;

            // Rest the water height and material.
            VirtualMap.ResetWater();

            ShaderGlobals.WindMin = xmlWorldData.windMin;
            ShaderGlobals.WindMax = xmlWorldData.windMax;

            /// Setting these will also merge in the new data from xmlWorldData.
            BokuGame.Audio.SetVolume("Foley", xmlWorldData.foleyVolume * XmlOptionsData.FoleyVolume);
            BokuGame.Audio.SetVolume("Music", xmlWorldData.musicVolume * XmlOptionsData.MusicVolume);
        }

        /// <summary>
        /// Based on the PreGame setting in xmlWorldData, 
        /// sets up the correct pregame object.
        /// </summary>
        public void SetUpPreGame()
        {
            // Ensure that any existing pregame has been deactivated.
            if (preGame != null && preGame.Active)
            {
                preGame.Active = false;
            }

            // First, see if this world uses a PreGame object.  If so, create
            // a new one if needed or reuse the existing one.
            //
            // Note that I'm a bit of an idiot so when I first put this in I used
            // localized strings for the values.  Which means they don't trigger if 
            // a different language is used.  To be fully back compatible we'd need
            // to test for every language hence TranslatePregameToEnglish().
            // Eventually, everyone will be using the hard-coded values and all
            // will be right in the world.
            xmlWorldData.preGame = TranslatePregameToEnglish(xmlWorldData.preGame);

            if (xmlWorldData.preGame != null)
            {
                if (xmlWorldData.preGame == "Countdown" || xmlWorldData.preGame == Strings.Localize("editWorldParams.countdown"))
                {
                    PreGameRacing pre = preGame as PreGameRacing;
                    if (pre == null)
                    {
                        preGame = new PreGameRacing();
                    }
                }
                else if (xmlWorldData.preGame == "Description with Countdown" || xmlWorldData.preGame == Strings.Localize("editWorldParams.countdownWithDesc"))
                {
                    PreGameRacingWithDesc pre = preGame as PreGameRacingWithDesc;
                    if (pre == null)
                    {
                        preGame = new PreGameRacingWithDesc();
                    }
                }
                else if (xmlWorldData.preGame == "World Description" || xmlWorldData.preGame == Strings.Localize("editWorldParams.levelDesc"))
                {
                    PreGameDesc pre = preGame as PreGameDesc;
                    if (pre == null)
                    {
                        preGame = new PreGameDesc();
                    }
                    pre = preGame as PreGameDesc;
                    pre.Logo = xmlWorldData.preGameLogo;
                }
                else if (xmlWorldData.preGame == "World Title" || xmlWorldData.preGame == Strings.Localize("editWorldParams.levelTitle"))
                {
                    PreGameTitle pre = preGame as PreGameTitle;
                    if (pre == null)
                    {
                        preGame = new PreGameTitle();
                    }
                }
                else
                {
                    preGame = null;
                }

                // Only activate the pregame if we have one and we're running.
                if (preGame != null && InGame.inGame.State == States.Active && updateObj == runSimUpdateObj)
                {
                    preGame.Active = true;
                }
            }
            else
            {
                preGame = null;
            }

        }   // end of SetUpPreGame()

        /// <summary>
        /// Loads the level.
        /// </summary>
        /// <param name="levelFullPath">The full path of the XmlWorldData file.</param>
        /// <param name="keepPersistentScores">Used when loading linked levels to preserve persistent scores.</param>
        /// <param name="newWorld">If New World was chosen from either Main Menu or Home Menu.</param>
        /// <param name="andRun">If true, we're loading a world to be run.  If false, loading for edit.</param>
        /// <returns>True if level is successfully loaded, false otherwise.</returns>
        bool LoadLevel(string levelFullPath, bool keepPersistentScores, bool newWorld, bool andRun)
        {
            // Before loading, unreference the old level and run a garbage collection pass.
            // This avoids having two level's worth of data loaded in memory at the same time
            // between now and the next automatic GC pass.
            xmlWorldData = null;
            xmlLevelData = null;
            GC.Collect();

            // Calling LoadLevel implies that we want to restart from disk and discard
            // any changes we have made.  So, reload the world file and null the stuff
            // filename so that it is forced to reload when ResetSim is called.
            xmlWorldDataFullPath = levelFullPath;

            // Load the world data from the file.
            xmlWorldData = XmlWorldData.Load(xmlWorldDataFullPath, XnaStorageHelper.Instance);
            if (xmlWorldData == null)
            {
                return false;
            }

            if (newWorld)
            {
                xmlWorldData.genres = (int)Genres.MyWorlds;
            }
            else
            {
                //check special flags that aren't properly persisted...
                if (XmlDataHelper.CheckWorldExistsByGenre(xmlWorldData.id, Genres.Downloads))
                {
                    xmlWorldData.genres |= (int)Genres.Downloads;
                }
                else if (XmlDataHelper.CheckWorldExistsByGenre(xmlWorldData.id, Genres.BuiltInWorlds))
                {
                    xmlWorldData.genres |= (int)Genres.BuiltInWorlds;
                }
                else
                {
                    xmlWorldData.genres |= (int)Genres.MyWorlds;
                }
            }


            Instrumentation.RecordEvent(Instrumentation.EventId.LevelLoaded, xmlWorldData.id.ToString());

            xmlLevelDataFullPath = null;

            // Reset focus actor
            InGame.inGame.mouseEditUpdateObj.ToolBox.EditObjectsToolInstance.FocusActor = null;
            InGame.inGame.touchEditUpdateObj.ToolBox.EditObjectsToolInstance.FocusActor = null;

            // Flush out any previous level state and load the new.
            // Load the level state.
            ResetSim(preserveScores: false, removeCreatablesFromScene: andRun == true, keepPersistentScores: keepPersistentScores);

            InGame.ResetLevelLoadedTime();

            //
            // HACK HACK HACK
            //
            // Hack loaded actors.  Note that any kode change hacks should be done in Reflex.Fixup().
            //

            // Fix pipe rotations.  The original code didn't snap properly in all directions.
            // After it was fixed, pipes from old levels come in as rotated.  So, this tweaks
            // them back into shape.
            if (string.Compare(xmlWorldData.BokuVersion, "1.5.0.0") < 0)
            {
                foreach (GameThing thing in InGame.inGame.gameThingList)
                {
                    if (thing.Chassis != null && thing.Chassis is SimWorld.Chassis.PipeChassis && thing.DisplayNameNumber.Contains("Pipe"))
                    {
                        Movement m = thing.Movement;
                        if (MyMath.Equals(m.RotationZ, 2.2f, 0.1f))
                        {
                            m.RotationZ = MathHelper.PiOver2;
                        }
                        if (MyMath.Equals(m.RotationZ, 0.63f, 0.1f))
                        {
                            m.RotationZ = 0;
                        }
                    }
                }
            }


            // If requested, start in run sim mode.
            if (andRun)
            {
                Activate();
                shared.refreshThumbnail = true;
                CurrentUpdateMode = UpdateMode.RunSim;

                // Start camera in edit mode.  This will automatically be overridden
                // to Actor mode if there is a user controlled actor.
                CameraInfo.Mode = CameraInfo.Modes.Edit;

                // If there's a starting camera, use that.  If not, use the PlayMode
                // camera which should be where the camera was when the level was saved.
                if (StartingCamera)
                {
                    RestoreStartingCamera();
                }
                else if (Terrain.Current.FixedCamera)
                {
                    RestoreFixedCamera();
                }
                else
                {
                    RestorePlayModeCamera();
                }
            }
            else
            {
                Activate();
                PauseAllGameThings();
                RestoreEditCamera(false);
            }

            return true;

        }   // end of InGame LoadLevel()

        Dictionary<string, string> preGameDict;

        /// <summary>
        /// Takes the string from XmlWorldData and tries to convert
        /// it to English.  This is a bit of a hack to fix up older
        /// levels where this string was localized when it shouldn't 
        /// have been.
        /// </summary>
        /// <param name="preGame"></param>
        /// <returns>Translated stirng if found, original string if not found.</returns>
        string TranslatePregameToEnglish(string preGame)
        {
            // Lazy init.
            if (preGameDict == null)
            {
                preGameDict = new Dictionary<string, string>();

                // Note: commented out entries are duplicates from other 
                // languages which will cause the Dictionary to throw.
                
                // English (not needed, but will exercise the path...
                preGameDict.Add("Nothing", "Nothing");
                preGameDict.Add("Countdown", "Countdown");
                preGameDict.Add("Description with Countdown", "Description with Countdown");
                preGameDict.Add("World Title", "World Title");
                preGameDict.Add("World Description", "World Description");

                // Arabic, AR
                preGameDict.Add("لا شيء", "Nothing");
                preGameDict.Add("عد تنازلي", "Countdown");
                preGameDict.Add("وصف مع عد تنازلي", "Description with Countdown");
                preGameDict.Add("عنوان العالم", "World Title");
                preGameDict.Add("وصف العالم", "World Description");

                // Czech, CS
                preGameDict.Add("Ničím", "Nothing");
                preGameDict.Add("Odpočítáváním", "Countdown");
                preGameDict.Add("Popisem a odpočítáváním", "Description with Countdown");
                preGameDict.Add("Názvem světa", "World Title");
                preGameDict.Add("Popisem světa", "World Description");

                // Welsh, CY
                preGameDict.Add("Dim byd", "Nothing");
                //preGameDict.Add("Countdown", "Countdown");
                preGameDict.Add("Disgrifiad gyda Countdown", "Description with Countdown");
                preGameDict.Add("Teitl y Byd", "World Title");
                preGameDict.Add("Disgrifiad y Byd", "World Description");

                // German, DE
                preGameDict.Add("Nichts", "Nothing");
                preGameDict.Add("Runterzählen/Countdown", "Countdown");
                preGameDict.Add("Beschreibung mit Countdown", "Description with Countdown");
                preGameDict.Add("Name der Welt", "World Title");
                preGameDict.Add("Beschreibung der Welt", "World Description");

                // Greek, EL
                preGameDict.Add("Τίποτα", "Nothing");
                preGameDict.Add("Αντίστροφη Μέτρηση", "Countdown");
                preGameDict.Add("Περιγραφή με Αντίστροφη Μέτρηση", "Description with Countdown");
                preGameDict.Add("Τίτλος Κόσμου", "World Title");
                preGameDict.Add("Περιγραφή Κόσμου", "World Description");

                // Spanish, ES
                preGameDict.Add("Nada", "Nothing");
                preGameDict.Add("Cuenta Regresiva", "Countdown");
                preGameDict.Add("Descripción con Cuenta Regresiva", "Description with Countdown");
                preGameDict.Add("Título del Mundo", "World Title");
                preGameDict.Add("Descripción del Mundo", "World Description");

                // Basque, EU
                preGameDict.Add("Ezerekin", "Nothing");
                preGameDict.Add("Atzerantzko zenbaketa", "Countdown");
                preGameDict.Add("Deskripzioa atzerantzko zenbaketaz", "Description with Countdown");
                preGameDict.Add("Munduaren Tituloa", "World Title");
                preGameDict.Add("Munduaren deskripzioa", "World Description");

                // French, FR
                preGameDict.Add("Rien", "Nothing");
                preGameDict.Add("Compte à rebours", "Countdown");
                preGameDict.Add("Description avec compte à rebours", "Description with Countdown");
                preGameDict.Add("Titre du monde", "World Title");
                preGameDict.Add("Description du monde", "World Description");

                // Hebrew, HE
                preGameDict.Add("כלום", "Nothing");
                preGameDict.Add("ספירה לאחור", "Countdown");
                preGameDict.Add("תיאור עם ספירה לאחור", "Description with Countdown");
                preGameDict.Add("כותרת העולם", "World Title");
                preGameDict.Add("תיאור העולם", "World Description");

                // Hungarian, HU
                preGameDict.Add("Semmi", "Nothing");
                preGameDict.Add("Visszaszámlálás", "Countdown");
                preGameDict.Add("Leírás visszaszámlálással", "Description with Countdown");
                preGameDict.Add("Világ címe", "World Title");
                preGameDict.Add("Világ leírása", "World Description");

                // Icelandic, IS
                preGameDict.Add("Enginn", "Nothing");
                preGameDict.Add("Niðurtalning", "Countdown");
                preGameDict.Add("Lýsing ásamt niðurtalningu", "Description with Countdown");
                preGameDict.Add("Heiti heimsins", "World Title");
                preGameDict.Add("Lýsing heimsins", "World Description");

                // Italian, IT
                preGameDict.Add("Nulla", "Nothing");
                //preGameDict.Add("Countdown", "Countdown");
                preGameDict.Add("Descrizione con Countdown", "Description with Countdown");
                preGameDict.Add("Titolo del Mondo", "World Title");
                preGameDict.Add("Descrizione del Mondo", "World Description");

                // Japanese, JA
                preGameDict.Add("なにもなし", "Nothing");
                preGameDict.Add("カウントダウン", "Countdown");
                preGameDict.Add("せつめいとカウントダウン", "Description with Countdown");
                preGameDict.Add("ワールドの名前", "World Title");
                preGameDict.Add("ワールドのせつめい", "World Description");

                // Korean, KO
                preGameDict.Add("없음", "Nothing");
                preGameDict.Add("카운트다운", "Countdown");
                preGameDict.Add("설명과 카운트다운", "Description with Countdown");
                preGameDict.Add("월드 제목", "World Title");
                preGameDict.Add("월드 설명", "World Description");

                // Lithuanian, LT
                preGameDict.Add("Nieko", "Nothing");
                preGameDict.Add("Starto atskaitą", "Countdown");
                preGameDict.Add("Aprašymą su atskaita", "Description with Countdown");
                preGameDict.Add("Pasaulio antraštę", "World Title");
                preGameDict.Add("Pasaulio aprašymą", "World Description");

                // Dutch, NL
                preGameDict.Add("Niets", "Nothing");
                preGameDict.Add("Aftellen", "Countdown");
                preGameDict.Add("Beschrijving en aftellen", "Description with Countdown");
                preGameDict.Add("Titel van wereld", "World Title");
                preGameDict.Add("Beschrijving van wereld", "World Description");

                // Norwegian, NO
                preGameDict.Add("Ingenting", "Nothing");
                preGameDict.Add("Nedtelling", "Countdown");
                preGameDict.Add("Beskrivelse av nedtelling", "Description with Countdown");
                preGameDict.Add("Verdenens navn", "World Title");
                preGameDict.Add("Verdenens beskrivelse", "World Description");

                // Polish, PL
                preGameDict.Add("Niczym", "Nothing");
                preGameDict.Add("Odliczaniem", "Countdown");
                preGameDict.Add("Opis z odliczaniem", "Description with Countdown");
                preGameDict.Add("Tytuł poziomu", "World Title");
                preGameDict.Add("Opis poziomu", "World Description");

                // Portuguese, PT
                //preGameDict.Add("Nada", "Nothing");
                preGameDict.Add("Contagem Decrescente", "Countdown");
                preGameDict.Add("Descrição com Contagem Decrescente", "Description with Countdown");
                preGameDict.Add("Título do Mundo", "World Title");
                preGameDict.Add("Descrição do Mundo", "World Description");

                // Russian, RU
                preGameDict.Add("Ничего", "Nothing");
                preGameDict.Add("Отсчет", "Countdown");
                preGameDict.Add("Описание с отсчетом", "Description with Countdown");
                preGameDict.Add("Название мира", "World Title");
                preGameDict.Add("Описание мира", "World Description");

                // Turkish, TR
                preGameDict.Add("Hiçbir şey", "Nothing");
                preGameDict.Add("Geri sayım", "Countdown");
                preGameDict.Add("Geri Sayım ile Tanımlama", "Description with Countdown");
                preGameDict.Add("Dünya Başlığı", "World Title");
                preGameDict.Add("Dünya Tarifi", "World Description");

                // Chinese (simplified), ZH-CN
                preGameDict.Add("无", "Nothing");
                preGameDict.Add("倒数", "Countdown");
                preGameDict.Add("倒数说明", "Description with Countdown");
                preGameDict.Add("世界标题", "World Title");
                preGameDict.Add("世界说明", "World Description");

                // Chinese (traditional), ZH-TW
                preGameDict.Add("無", "Nothing");
                preGameDict.Add("倒數", "Countdown");
                preGameDict.Add("倒數說明", "Description with Countdown");
                preGameDict.Add("世界標題", "World Title");
                preGameDict.Add("世界說明", "World Description");
            
            }

            preGame = preGame.Trim();

            string result = null;
            if (preGameDict.TryGetValue(preGame, out result))
            {
                if (!string.IsNullOrWhiteSpace(result))
                {
                    preGame = result;
                }
            }

            return preGame;
        }   // end of TranslatePregameToEnglish()

        #endregion

    }   // end of class InGame


}   // end of namespace Boku
