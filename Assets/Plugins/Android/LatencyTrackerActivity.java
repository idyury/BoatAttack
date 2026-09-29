package com.unity3d.player;

import android.os.Bundle;
import android.view.InputDevice;
import android.view.KeyEvent;
import android.view.MotionEvent;
import android.view.Surface;
import android.view.SurfaceHolder;
import android.view.SurfaceView;
import android.view.View;
import android.view.ViewGroup;

/**
 * Unity 2022.3 activity bridge for the Android Surface timestamp backend.
 *
 * Place this file at:
 * Assets/Plugins/Android/com/unity3d/player/LatencyTrackerActivity.java
 * and select this activity in the AndroidManifest.
 */
public class LatencyTrackerActivity extends UnityPlayerActivity
        implements SurfaceHolder.Callback {
    private static final int INPUT_TOUCH_DOWN = 0;
    private static final int INPUT_MOUSE_DOWN = 1;
    private static final int INPUT_KEYBOARD_DOWN = 2;
    private static final int INPUT_GAMEPAD_BUTTON_DOWN = 3;

    static {
        System.loadLibrary("GfxPluginUnityLatencyTracker");
    }

    private static native void nativeOnMotionInputDown(
            long eventTimeNs, long dispatchTimeNs, int inputType,
            int inputCode, int inputSource, int inputDeviceId);
    private static native void nativeOnKeyInputDown(
            KeyEvent event, long dispatchTimeNs, int inputType,
            int inputCode, int inputSource, int inputDeviceId);
    private static native void nativeSetSurface(Surface surface);

    private SurfaceHolder latencyTrackerSurfaceHolder;
    private int surfaceSearchAttempts;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        attachToUnitySurface();
    }

    private void attachToUnitySurface() {
        SurfaceView surfaceView = findSurfaceView(mUnityPlayer);
        if (surfaceView == null) {
            // Unity can attach the rendering view asynchronously on some
            // configurations. Retry on the UI queue for a bounded period.
            if (++surfaceSearchAttempts <= 120) {
                mUnityPlayer.postDelayed(this::attachToUnitySurface, 16);
            }
            return;
        }

        latencyTrackerSurfaceHolder = surfaceView.getHolder();
        latencyTrackerSurfaceHolder.addCallback(this);
        Surface surface = latencyTrackerSurfaceHolder.getSurface();
        if (surface != null && surface.isValid()) {
            nativeSetSurface(surface);
        }
    }

    private static SurfaceView findSurfaceView(View view) {
        if (view instanceof SurfaceView) {
            return (SurfaceView) view;
        }
        if (view instanceof ViewGroup) {
            ViewGroup group = (ViewGroup) view;
            for (int i = 0; i < group.getChildCount(); ++i) {
                SurfaceView result = findSurfaceView(group.getChildAt(i));
                if (result != null) {
                    return result;
                }
            }
        }
        return null;
    }

    @Override
    public void surfaceCreated(SurfaceHolder holder) {
        nativeSetSurface(holder.getSurface());
    }

    @Override
    public void surfaceChanged(
            SurfaceHolder holder, int format, int width, int height) {
        nativeSetSurface(holder.getSurface());
    }

    @Override
    public void surfaceDestroyed(SurfaceHolder holder) {
        nativeSetSurface(null);
    }

    @Override
    public boolean dispatchTouchEvent(MotionEvent event) {
        // Record only the first down transition. MOVE, UP, CANCEL, and
        // additional fingers are deliberately ignored.
        if (event.getActionMasked() == MotionEvent.ACTION_DOWN) {
            int inputType = event.isFromSource(InputDevice.SOURCE_MOUSE)
                    ? INPUT_MOUSE_DOWN
                    : INPUT_TOUCH_DOWN;
            nativeOnMotionInputDown(
                    event.getEventTime() * 1_000_000L,
                    System.nanoTime(),
                    inputType,
                    event.getButtonState(),
                    event.getSource(),
                    event.getDeviceId());
        }
        return super.dispatchTouchEvent(event);
    }

    @Override
    public boolean dispatchKeyEvent(KeyEvent event) {
        // A held key produces repeated ACTION_DOWN events. repeatCount == 0
        // limits tracking to the initial physical button-down transition.
        if (event.getAction() == KeyEvent.ACTION_DOWN
                && event.getRepeatCount() == 0) {
            InputDevice inputDevice = event.getDevice();
            int deviceSources = inputDevice != null
                    ? inputDevice.getSources()
                    : event.getSource();
            boolean fromMouse =
                    event.isFromSource(InputDevice.SOURCE_MOUSE);
            boolean fromGamepad =
                    KeyEvent.isGamepadButton(event.getKeyCode())
                    || event.isFromSource(InputDevice.SOURCE_GAMEPAD)
                    || event.isFromSource(InputDevice.SOURCE_JOYSTICK)
                    || (deviceSources & InputDevice.SOURCE_GAMEPAD)
                            == InputDevice.SOURCE_GAMEPAD
                    || (deviceSources & InputDevice.SOURCE_JOYSTICK)
                            == InputDevice.SOURCE_JOYSTICK;
            int inputType = fromMouse
                    ? INPUT_MOUSE_DOWN
                    : (fromGamepad
                            ? INPUT_GAMEPAD_BUTTON_DOWN
                            : INPUT_KEYBOARD_DOWN);
            nativeOnKeyInputDown(
                    event,
                    System.nanoTime(),
                    inputType,
                    event.getKeyCode(),
                    event.getSource(),
                    event.getDeviceId());
        }
        return super.dispatchKeyEvent(event);
    }

    @Override
    protected void onDestroy() {
        if (latencyTrackerSurfaceHolder != null) {
            latencyTrackerSurfaceHolder.removeCallback(this);
            latencyTrackerSurfaceHolder = null;
        }
        nativeSetSurface(null);
        super.onDestroy();
    }
}
