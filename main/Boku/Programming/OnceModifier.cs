using System;
using System.Collections.Generic;

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

namespace Boku.Programming
{
    /// <summary>
    /// If present, the associated sensor will operate under "once" rules:
    /// - when a rule's condition becomes true, the actuator fires normally.
    /// - the rule is then suspended until the rule's condition becomes false.
    /// - when the rule's condition becomes false, it is re-enabled.
    /// - when it subsequently becomes true, it fires normally, is suspended again, and so on.
    /// </summary>
    public class OnceModifier : Modifier
    {
        /// <summary>
        /// This value is set on the "when" side of the evaluation, if the sensor condition was met.
        /// </summary>
        [XmlIgnore]
        public bool fired;

        /// <summary>
        /// This value is incremented on the "do" side of the evaluation, if the effector generated by the reflex was acted on.
        /// </summary>
        [XmlIgnore]
        public int _applyCount;
        public int applyCount
        {
            get { return _applyCount; }
            set { _applyCount = value; fireCount += value != 0 ? 1 : 0; }
        }

        /// <summary>
        /// This value represents the number of times the reflex we are modifiying has actually executed.
        /// </summary>
        [XmlIgnore]
        public int fireCount;

        #region Accessors

        /// <summary>
        /// This value is set on the "when" side of the evaluation, if the sensor condition was met.
        /// </summary>
        [XmlIgnore]
        public bool Fired
        {
            get { return fired; }
            set { fired = value; }
        }

        #endregion

        #region Public
        public override ProgrammingElement Clone()
        {
            OnceModifier clone = new OnceModifier();
            CopyTo(clone);
            return clone;
        }

        protected void CopyTo(OnceModifier clone)
        {
            base.CopyTo(clone);
        }

        public override void PostProcessAction(bool firing, Reflex reflex, ref bool action)
        {
            if (reflex.Sensor is TimerSensor && fireCount > 0)
            {
                // Special handling for timers. After they fire once, supress them forever.
                firing = false;
            }

            if (applyCount > 0)
            {
                // Only start supressing the action after the reflex has executed one time.
                action = false;
            }
            else if (!Fired)
            {
                action = firing;
            }

            Fired = firing;
        }

        public override void Reset(Reflex reflex)
        {
            Fired = false;
            applyCount = 0;
            fireCount = 0;
            base.Reset(reflex);
        }

        #endregion
    }
}
