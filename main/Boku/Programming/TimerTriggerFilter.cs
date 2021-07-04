// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

using System.Xml;
using System.Xml.Serialization;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Storage;

using Boku.Base;
using Boku.Common;
using Boku.Input;

namespace Boku.Programming
{
    /// <summary>
    /// Filter that manages the timer state and returns a positive action when
    /// time defined by other filters has been passed
    /// 
    /// </summary>
    public class TimerTriggerFilter : Filter
    {
        protected Boku.Base.GameTimer timer = new Boku.Base.GameTimer(Boku.Base.GameTimer.ClockType.GameClock, 1.0);
        protected double timerRandom = 0;
        protected double timerBase = 0;
        protected int prevFrame = 0;            // Frame # when last updated.

        public override ProgrammingElement Clone()
        {
            TimerTriggerFilter clone = new TimerTriggerFilter();
            CopyTo(clone);
            return clone;
        }

        protected void CopyTo(TimerTriggerFilter clone)
        {
            base.CopyTo(clone);
        }

        public override bool MatchTarget(Reflex reflex, SensorTarget sensorTarget)
        {
            return true;
        }

        public override bool MatchAction(Reflex reflex, out object param)
        {
            param = null;
            bool match = false;

            if (prevFrame + 1 == Time.NonPausedFrameCounter)
            {
                // Running continuously.
                if (timer.Elapsed)
                {
                    // Reset the duration for the timer if we have any random amount.  
                    // This way each time the timer triggers a new random duration is set.
                    if (timerRandom != 0)
                    {
                        timer.Duration = CalcTimerValue();
                    }
                    timer.ReStart(sync: true);
                    match = true;
                }
            }
            else
            {
                // First frame, need to restart.
                timer.ReStart(sync: false);
            }

            prevFrame = Time.NonPausedFrameCounter;

            return match;

        }   // end of MatchAction()

        public override void Reset(Reflex reflex)
        {
            timerBase = 0;
            timerRandom = 0;
            bool randomScore = false;

            timer.Stop();

            // Walk filters and set params if found.
            //
            // TODO (scoy) We could expand timers so they encompass the 
            // full range of numeric inputs, eg scores, health, etc.
            //
            for (int indexFilter = 0; indexFilter < reflex.Filters.Count; indexFilter++)
            {
                Filter filter = reflex.Filters[indexFilter] as Filter;
                if (filter != null)
                {
                    if (filter is TimerFilter)
                    {
                        TimerFilter timerFilter = filter as TimerFilter;
                        if (randomScore)
                        {
                            timerRandom += timerFilter.seconds;
                        }
                        else
                        {
                            timerBase += timerFilter.seconds;
                        }
                    }
                    if (filter is RandomFilter)
                    {
                        randomScore = true;
                    }
                }
            }

            // Force random to default to 5.
            if (randomScore && timerRandom == 0)
            {
                timerRandom = 5;
            }

            timer.Reset(CalcTimerValue());
            timer.Stop(); // make sure it is paused/stopped

            base.Reset(reflex);
        }

        protected double CalcTimerValue()
        {
            return timerBase + timerRandom * BokuGame.bokuGame.rnd.NextDouble();
        }
    }
}
