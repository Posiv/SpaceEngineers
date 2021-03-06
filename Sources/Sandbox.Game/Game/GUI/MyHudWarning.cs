﻿using Sandbox;
using Sandbox.Common;
using Sandbox.Engine.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Gui;
using Sandbox.Game.GUI;
using Sandbox.Game.Localization;
using Sandbox.Game.World;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using VRage;
using VRage.Audio;
using VRage.Library.Utils;
using VRage.Utils;
using VRage.Utils;
using VRageMath;

namespace Sandbox.Game.Gui
{
    /// <summary>
    /// Delegate of warning detection method
    /// </summary>
    /// <returns></returns>
    delegate bool MyWarningDetectionMethod(out MyGuiSounds cue, out MyStringId text);

    /// <summary>
    /// This class represents HUD warning
    /// </summary>
    class MyHudWarning : MyHudNotification
    {
        /// <summary>
        /// Warning's priority
        /// </summary>
        public int WarningPriority { get; private set; }
        public int RepeatInterval;
        public Func<bool> CanPlay;
        public Action Played;

        private MyWarningDetectionMethod m_warningDetectionMethod;
        private enum WarningState { NOT_STARTED, STARTED, PLAYED };
        private WarningState m_warningState;

        private bool m_warningDetected;
        private  int m_msSinceLastStateChange;
        private  int m_soundDelay;

        /// <summary>
        /// Creates new instance of HUD warning
        /// </summary>
        /// <param name="detectionMethod">Warning's detection method</param>
        /// <param name="soundWarning">Sound warning</param>
        /// <param name="textWarning">Text warning</param>
        /// <param name="priority">Warning's priority</param>
        public MyHudWarning(MyWarningDetectionMethod detectionMethod, int priority, int repeatInterval = 0, int soundDelay = 0, int disappearTime = 0)
            : base(disappearTimeMs: disappearTime, font: MyFontEnum.Red , level: MyNotificationLevel.Important)
        {
            m_warningDetectionMethod = detectionMethod;
            RepeatInterval = repeatInterval;
            m_soundDelay = soundDelay;
            WarningPriority = priority;
            m_warningDetected = false;
        }

        /// <summary>
        /// Call it in each update
        /// </summary>
        /// <param name="isWarnedHigherPriority">Indicated if warning with greater priority was signalized</param>
        /// <returns>Returns true if warning detected. Else returns false</returns>
        public bool Update(bool isWarnedHigherPriority)
        {
            MyGuiSounds cue = MyGuiSounds.None;
            MyStringId text = MySpaceTexts.Blank;
            m_warningDetected = false;
            if (!isWarnedHigherPriority)
                m_warningDetected = m_warningDetectionMethod(out cue, out text);

            m_msSinceLastStateChange += MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS * MyHudWarnings.FRAMES_BETWEEN_UPDATE;
            if (m_warningDetected)
            {
                switch (m_warningState)
                {
                    case WarningState.NOT_STARTED:
                        Text = text;
                        MyHud.Notifications.Add(this);
                        m_msSinceLastStateChange = 0;
                        m_warningState = WarningState.STARTED;
                        break;
                    case WarningState.STARTED:
                        if (m_msSinceLastStateChange >= m_soundDelay && CanPlay())
                        {
                            MyHudWarnings.EnqueueSound(cue);
                            m_warningState = WarningState.PLAYED;
                            Played();
                        }
                        break;
                    case WarningState.PLAYED:
                        if (RepeatInterval > 0)
                        {
                            if (CanPlay())
                            {
                                MyHud.Notifications.Remove(this);
                                MyHud.Notifications.Add(this);
                                MyHudWarnings.EnqueueSound(cue);
                                Played();
                            }
                        }
                        break;
                }
            }
            else
            {
                MyHud.Notifications.Remove(this);
                MyHudWarnings.RemoveSound(cue);
                m_warningState = WarningState.NOT_STARTED;
            }
            return m_warningDetected;
        }

        /// <summary>
        /// Draws warning's text if any warning detected
        /// </summary>
    }

    /// <summary>
    /// This class represents HUD warning group. Only 1 warning can be signalized, from this group.
    /// </summary>
    class MyHudWarningGroup
    {
        private List<MyHudWarning> m_hudWarnings;
        private bool m_canBeTurnedOff;
        private int m_msSinceLastCuePlayed;
        private int m_highestWarnedPriority = int.MaxValue;

        /// <summary>
        /// Creates new instance of HUD warning group
        /// </summary>
        /// <param name="hudWarnings"></param>
        public MyHudWarningGroup(List<MyHudWarning> hudWarnings, bool canBeTurnedOff)
        {
            m_hudWarnings = new List<MyHudWarning>(hudWarnings);
            SortByPriority();
            m_canBeTurnedOff = canBeTurnedOff;
            InitLastCuePlayed();
            foreach (var warning in hudWarnings)
            {
                warning.CanPlay = () => m_highestWarnedPriority > warning.WarningPriority || (m_msSinceLastCuePlayed > warning.RepeatInterval && m_highestWarnedPriority == warning.WarningPriority);
                warning.Played = () =>
                    {
                        m_msSinceLastCuePlayed = 0;
                        m_highestWarnedPriority = warning.WarningPriority;
                    };
            }
        }

        private void InitLastCuePlayed()
        {
            foreach (var warning in m_hudWarnings)
                if (warning.RepeatInterval > m_msSinceLastCuePlayed)
                    m_msSinceLastCuePlayed = warning.RepeatInterval;
        }

        /// <summary>
        /// Call it in each update.
        /// </summary>
        public void Update()
        {
            //if (m_canBeTurnedOff && MyConfig.Notifications == false)
            //    return;
            if (!MySandboxGame.IsGameReady)
                return;
            m_msSinceLastCuePlayed += MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS * MyHudWarnings.FRAMES_BETWEEN_UPDATE;
            bool isWarnedHigherPriority = false;
            foreach (MyHudWarning hudWarning in m_hudWarnings)
                if (hudWarning.Update(isWarnedHigherPriority))
                    isWarnedHigherPriority = true;
            if (!isWarnedHigherPriority)
                m_highestWarnedPriority = int.MaxValue;
        }

        /// <summary>
        /// Adds new HUD warning to this group
        /// </summary>
        /// <param name="hudWarning">HUD warning to add</param>
        public void Add(MyHudWarning hudWarning)
        {
            m_hudWarnings.Add(hudWarning);
            SortByPriority();
            InitLastCuePlayed();
            hudWarning.CanPlay = () => m_highestWarnedPriority > hudWarning.WarningPriority|| (m_msSinceLastCuePlayed > hudWarning.RepeatInterval && m_highestWarnedPriority == hudWarning.WarningPriority);
            hudWarning.Played = () =>
                {
                    m_msSinceLastCuePlayed = 0;
                    m_highestWarnedPriority = hudWarning.WarningPriority;
                };
        }

        /// <summary>
        /// Removes HUD warning from this group
        /// </summary>
        /// <param name="hudWarning">HUD warning to remove</param>
        public void Remove(MyHudWarning hudWarning)
        {
            m_hudWarnings.Remove(hudWarning);
        }

        /// <summary>
        /// Removes all HUD warnings from this group
        /// </summary>        
        public void Clear()
        {
            m_hudWarnings.Clear();
        }

        private void SortByPriority()
        {
            m_hudWarnings.Sort((x, y) => x.WarningPriority.CompareTo(y.WarningPriority));
        }
    }

    /// <summary>
    /// This class represents HUD warnings for entities
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    class MyHudWarnings : MySessionComponentBase
    {
        public static readonly int FRAMES_BETWEEN_UPDATE = 30;

        private static List<MyHudWarningGroup> m_hudWarnings = new List<MyHudWarningGroup>();
        private static List<MyGuiSounds> m_soundQueue = new List<MyGuiSounds>();
        private static IMySourceVoice m_sound;
        private static int m_lastSoundPlayed = 0;
        private int m_updateCounter = 0;

        public static void EnqueueSound(MyGuiSounds sound)
        {
            if (!MyGuiAudio.HudWarnings)
                return;
            if ((m_sound == null || !m_sound.IsPlaying) && MySandboxGame.TotalGamePlayTimeInMilliseconds - m_lastSoundPlayed > 5000)
            {
                m_sound = MyGuiAudio.PlaySound(sound);
                m_lastSoundPlayed = MySandboxGame.TotalGamePlayTimeInMilliseconds;
            }
            else
                m_soundQueue.Add(sound);
        }

        public static void RemoveSound(MyGuiSounds cueEnum)
        {
            if (m_sound != null && m_sound.CueEnum == MyGuiAudio.GetCue(cueEnum))
                m_sound.Stop();
            m_soundQueue.RemoveAll(new System.Predicate<MyGuiSounds>((cue) => { return cue == cueEnum; }));
        }

        /// <summary>
        /// Register new HUD warning group for entity
        /// </summary>
        /// <param name="entity">Entity</param>
        /// <param name="hudWarningGroup">HUD warning group</param>
        public static void Add(MyHudWarningGroup hudWarningGroup)
        {
            m_hudWarnings.Add(hudWarningGroup);
        }

        /// <summary>
        /// Unregister HUD warning group for entity
        /// </summary>
        /// <param name="entity">Entity</param>
        /// <param name="hudWarningGroup">HUD warning group</param>
        public static void Remove(MyHudWarningGroup hudWarningGroup)
        {
            m_hudWarnings.Remove(hudWarningGroup);
        }

        public override void LoadData()
        {
            base.LoadData();
            if (MySandboxGame.IsDedicated)
                return;
            var list = new List<MyHudWarning>();
            //Health warnings
            var warning = new MyHudWarning((out MyGuiSounds cue, out MyStringId text) => 
                { cue = MyGuiSounds.HudVocHealthLow;  text = MySpaceTexts.NotificationHealthLow; return HealthWarningMethod(0.4f);},
                1, 60000, 0, 2500);
            list.Add(warning);
            warning = new MyHudWarning((out MyGuiSounds cue, out MyStringId text) =>
                { cue = MyGuiSounds.HudVocHealthCritical;  text = MySpaceTexts.NotificationHealthCritical; return HealthWarningMethod(0.2f); },
                 0, 30000, 0, 5000);
            list.Add(warning);
            var group = new MyHudWarningGroup(list, false);
            Add(group);
            list.Clear();
            //Energy warnings
            warning = new MyHudWarning(
                EnergyLowWarningMethod,
                2, 60000, 0, 2500);
            list.Add(warning);
            warning = new MyHudWarning(
               EnergyCritWarningMethod,
               1, 30000, 0 , 5000);
            list.Add(warning);
            warning = new MyHudWarning(
               EnergyNoWarningMethod,
               0, 10000, 0, 5000);
            list.Add(warning);
            group = new MyHudWarningGroup(list, false);
            Add(group);
            list.Clear();

            //Meteor storm
            warning = new MyHudWarning(MeteorInboundWarningMethod,
                0, 10 * 60 * 1000, 0, 5000);
            list.Add(warning);
            group = new MyHudWarningGroup(list, false);
            Add(group);

        }

        private static bool HealthWarningMethod(float treshold)
        {
            if (MySession.LocalCharacter != null)
            {
                return MySession.LocalCharacter.HealthRatio < treshold && !MySession.LocalCharacter.IsDead;
            }
            else
                return false;
        }

        private static bool IsEnergyUnderTreshold(int treshold)
        {
            if (MySession.Static.CreativeMode || MySession.ControlledEntity == null)
                return false;
            if (MySession.ControlledEntity.Entity is MyCharacter || MySession.ControlledEntity == null)
            {
                var character = MySession.LocalCharacter;
                if (character == null) return false;

                if (character.SuitBattery.PowerReceiver.CurrentInput > 0)
                    return false;
                return (character.SuitBattery.RemainingCapacity / MyEnergyConstants.BATTERY_MAX_CAPACITY) * 100 < treshold && !character.IsDead;
            }
            else if (MySession.ControlledEntity.Entity is MyCockpit)
            {
                var grid = (MySession.ControlledEntity.Entity as MyCockpit).CubeGrid;
                return MyHud.ShipInfo.FuelRemainingTime * 60 < treshold && grid.GridSystems.PowerDistributor.ProducersEnabled != MyMultipleEnabledEnum.AllDisabled && grid.GridSystems.PowerDistributor.ProducersEnabled != MyMultipleEnabledEnum.NoObjects;
            }
            else
                return false;
        }

        private static bool MeteorInboundWarningMethod(out MyGuiSounds cue, out MyStringId text)
        {
            cue = MyGuiSounds.HudVocMeteorInbound; 
            text = MySpaceTexts.NotificationMeteorInbound;
            if (MyMeteorShower.CurrentTarget.HasValue && MySession.ControlledEntity != null)
            {
                var dist = Vector3.Distance(MyMeteorShower.CurrentTarget.Value.Center, MySession.ControlledEntity.Entity.PositionComp.GetPosition());
                return dist < (2 * MyMeteorShower.CurrentTarget.Value.Radius) + 500;
            }
            return false;
        }

        private static bool EnergyLowWarningMethod(out MyGuiSounds cue, out MyStringId text)
        {
            cue = MyGuiSounds.None;
            text = MySpaceTexts.Blank;
            if(!IsEnergyUnderTreshold(5))
                return false;
            if (MySession.ControlledEntity.Entity is MyCharacter)
            {
                cue = MyGuiSounds.HudVocEnergyLow;
                if (MySession.LocalCharacter != null && MySession.LocalCharacter.Definition.NeedsOxygen && MySession.Static.Settings.EnableOxygen)
                {
                    text = MySpaceTexts.NotificationSuitEnergyLowNoDamage;
                }
                else
                {
                    text = MySpaceTexts.NotificationSuitEnergyLow;
                }
            }
            else if (MySession.ControlledEntity.Entity is MyCockpit)
            {
                if ((MySession.ControlledEntity.Entity as MyCockpit).CubeGrid.IsStatic)
                    cue = MyGuiSounds.HudVocStationFuelLow;
                else
                    cue = MyGuiSounds.HudVocShipFuelLow;
                if (MySession.LocalCharacter != null && MySession.LocalCharacter.Definition.NeedsOxygen && MySession.Static.Settings.EnableOxygen)
                {
                    text = MySpaceTexts.NotificationSuitEnergyLowNoDamage;
                }
                else
                {
                    text = MySpaceTexts.NotificationSuitEnergyLow;
                }
            }
            else
                return false;
            return true;
        }

        private static bool EnergyCritWarningMethod(out MyGuiSounds cue, out MyStringId text)
        {
            cue = MyGuiSounds.None;
            text = MySpaceTexts.Blank;
            if (!IsEnergyUnderTreshold(1))
                return false;
            if (MySession.ControlledEntity.Entity is MyCharacter || MySession.ControlledEntity == null)
            {
                cue = MyGuiSounds.HudVocEnergyCrit;
                if (MySession.LocalCharacter != null && MySession.LocalCharacter.Definition.NeedsOxygen && MySession.Static.Settings.EnableOxygen)
                {
                    text = MySpaceTexts.NotificationSuitEnergyCriticalNoDamage;
                }
                else
                {
                    text = MySpaceTexts.NotificationSuitEnergyCritical;
                }
            }
            else if (MySession.ControlledEntity.Entity is MyCockpit)
            {
                if ((MySession.ControlledEntity.Entity as MyCockpit).CubeGrid.IsStatic)
                    cue = MyGuiSounds.HudVocStationFuelCrit;
                else
                    cue = MyGuiSounds.HudVocShipFuelCrit;
                if (MySession.LocalCharacter != null && MySession.LocalCharacter.Definition.NeedsOxygen && MySession.Static.Settings.EnableOxygen)
                {
                    text = MySpaceTexts.NotificationSuitEnergyCriticalNoDamage;
                }
                else
                {
                    text = MySpaceTexts.NotificationSuitEnergyCritical;
                }
            }
            else
                return false;
            return true;
        }

        private static bool EnergyNoWarningMethod(out MyGuiSounds cue, out MyStringId text)
        {
            cue = MyGuiSounds.None;
            text = MySpaceTexts.Blank;
            if (!IsEnergyUnderTreshold(0))
                return false;
            if (MySession.ControlledEntity.Entity is MyCharacter)
            {
                cue = MyGuiSounds.HudVocEnergyNo;
                text = MySpaceTexts.NotificationEnergyNo;
            }
            else if (MySession.ControlledEntity.Entity is MyCockpit)
            {
                if ((MySession.ControlledEntity.Entity as MyCockpit).CubeGrid.IsStatic)
                    cue = MyGuiSounds.HudVocStationFuelNo;
                else
                    cue = MyGuiSounds.HudVocShipFuelNo;
                text = MySpaceTexts.NotificationFuelNo;
            }
            else
                return false;
            return true;
        }

        protected override void UnloadData()
        {
            base.UnloadData();
            foreach (var warningGroup in m_hudWarnings)
                warningGroup.Clear();
            m_hudWarnings.Clear();
            m_soundQueue.Clear();
            if (m_sound != null)
                m_sound.Stop(true);
        }

        public override void UpdateAfterSimulation()
        {
            base.UpdateAfterSimulation();

            if (MySandboxGame.IsDedicated)
            {
                m_hudWarnings.Clear();
                m_soundQueue.Clear();
                return;
            }

            m_updateCounter++;
            if (m_updateCounter % FRAMES_BETWEEN_UPDATE == 0)
            {
                foreach (var warningGroup in m_hudWarnings)
                    warningGroup.Update();
                if (m_soundQueue.Count > 0 && MySandboxGame.TotalGamePlayTimeInMilliseconds - m_lastSoundPlayed > 5000)
                {
                    m_lastSoundPlayed = MySandboxGame.TotalGamePlayTimeInMilliseconds;
                    m_sound = MyGuiAudio.PlaySound(m_soundQueue[0]);
                    m_soundQueue.RemoveAt(0);
                }
            }
        }
    }
}
