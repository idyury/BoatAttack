using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

[DefaultExecutionOrder(-32000)]
public sealed class InputLatencyTracker : MonoBehaviour
{
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_STANDALONE_WIN)
    [DllImport("GfxPluginUnityLatencyTracker")]
    private static extern void SetCurrentFrameInputNow(int frameId);

    [DllImport("GfxPluginUnityLatencyTracker")]
    private static extern IntPtr GetRenderEventFunc();

    private static readonly WaitForEndOfFrame EndOfFrame =
        new WaitForEndOfFrame();

    private IntPtr renderEventFunction;

    private void OnEnable()
    {
        renderEventFunction = GetRenderEventFunc();
        StartCoroutine(IssueEndOfFrameEvents());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private void Update()
    {
        // Android captures down transitions in LatencyTrackerActivity.
        // Windows subclasses the player window for pointer/keyboard input
        // and polls XInput here. Calling every Update binds queued inputs to
        // the first Unity frame that can react to them.
        SetCurrentFrameInputNow(Time.frameCount);
    }

    private IEnumerator IssueEndOfFrameEvents()
    {
        while (enabled)
        {
            // Issue exactly once after all cameras and UI for this frame.
            // The native callback executes on Unity's render thread before
            // the corresponding player present.
            yield return EndOfFrame;
            GL.IssuePluginEvent(renderEventFunction, Time.frameCount);
        }
    }
#endif
}
