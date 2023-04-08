// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;


namespace Boku.Common.Sharing
{
    /// <summary>
    /// A no-op operation. Useful for making a callback in the next frame.
    /// </summary>
    public class FrameDelayedOperation : AsyncOperation
    {
        int startFrame;

        int frames;

        public FrameDelayedOperation(AsyncOpCallback callback, object param, object pwner)
            : base(AsyncOperations.Null, callback, param, pwner)
        {
            RequiresSignIn = false;
        }

        public FrameDelayedOperation(int frames, AsyncOpCallback callback, object param, object pwner)
            : this(callback, param, pwner)
        {
            this.frames = frames;
        }

        public override void Dispose()
        {
        }

        protected override void IStart()
        {
            Debug.Assert(false);    // TODO (****) Is this really being used?  If not, remove.
            startFrame = Time.FrameCounter;
        }

        internal override void Update()
        {
            if (Time.FrameCounter - startFrame >= frames)
            {
                Complete(true);
            }
        }
    }
}
