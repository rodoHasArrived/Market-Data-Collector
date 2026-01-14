using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for handling swipe, flick, and touch gestures throughout the application.
/// Provides right-flick detection for triggering dropdown navigation menus.
/// </summary>
public sealed class GestureService
{
    private static GestureService? _instance;
    private static readonly object _lock = new();

    // Gesture detection thresholds
    private const double MinFlickDistance = 50.0;          // Minimum distance for a flick (in pixels)
    private const double MinFlickVelocity = 300.0;         // Minimum velocity (pixels/second)
    private const double MaxFlickDuration = 300.0;         // Maximum time for a flick (ms)
    private const double DirectionTolerance = 45.0;        // Tolerance angle in degrees
    private const double EdgeSwipeThreshold = 40.0;        // Edge detection zone width

    // Gesture tracking state
    private readonly Dictionary<uint, GestureTrackingState> _activeGestures = new();
    private readonly List<UIElement> _registeredElements = new();
    private bool _isEnabled = true;

    /// <summary>
    /// Gets the singleton instance of the GestureService.
    /// </summary>
    public static GestureService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new GestureService();
                }
            }
            return _instance;
        }
    }

    private GestureService()
    {
    }

    /// <summary>
    /// Gets or sets whether gesture detection is enabled.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    /// <summary>
    /// Registers an element for gesture detection.
    /// </summary>
    /// <param name="element">The UI element to monitor for gestures.</param>
    public void RegisterElement(UIElement element)
    {
        if (element == null || _registeredElements.Contains(element))
            return;

        element.PointerPressed += Element_PointerPressed;
        element.PointerMoved += Element_PointerMoved;
        element.PointerReleased += Element_PointerReleased;
        element.PointerCanceled += Element_PointerCanceled;
        element.ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
        element.ManipulationStarted += Element_ManipulationStarted;
        element.ManipulationDelta += Element_ManipulationDelta;
        element.ManipulationCompleted += Element_ManipulationCompleted;

        _registeredElements.Add(element);
    }

    /// <summary>
    /// Unregisters an element from gesture detection.
    /// </summary>
    /// <param name="element">The UI element to stop monitoring.</param>
    public void UnregisterElement(UIElement element)
    {
        if (element == null || !_registeredElements.Contains(element))
            return;

        element.PointerPressed -= Element_PointerPressed;
        element.PointerMoved -= Element_PointerMoved;
        element.PointerReleased -= Element_PointerReleased;
        element.PointerCanceled -= Element_PointerCanceled;
        element.ManipulationStarted -= Element_ManipulationStarted;
        element.ManipulationDelta -= Element_ManipulationDelta;
        element.ManipulationCompleted -= Element_ManipulationCompleted;

        _registeredElements.Remove(element);
    }

    #region Pointer Event Handlers

    private void Element_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!_isEnabled || sender is not UIElement element)
            return;

        var pointer = e.GetCurrentPoint(element);
        var pointerId = pointer.PointerId;

        _activeGestures[pointerId] = new GestureTrackingState
        {
            PointerId = pointerId,
            StartTime = DateTime.UtcNow,
            StartPosition = pointer.Position,
            CurrentPosition = pointer.Position,
            Element = element,
            IsFromEdge = IsFromLeftEdge(pointer.Position, element)
        };
    }

    private void Element_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isEnabled || sender is not UIElement element)
            return;

        var pointer = e.GetCurrentPoint(element);
        var pointerId = pointer.PointerId;

        if (_activeGestures.TryGetValue(pointerId, out var state))
        {
            state.CurrentPosition = pointer.Position;
            state.LastUpdateTime = DateTime.UtcNow;

            // Calculate current delta and check for potential flick
            var delta = new Vector2(
                (float)(state.CurrentPosition.X - state.StartPosition.X),
                (float)(state.CurrentPosition.Y - state.StartPosition.Y));

            // Raise progress event for visual feedback
            if (Math.Abs(delta.X) > 10)
            {
                var direction = GetFlickDirection(delta);
                var progress = Math.Min(1.0, Math.Abs(delta.X) / MinFlickDistance);

                FlickProgress?.Invoke(this, new FlickProgressEventArgs
                {
                    Element = element,
                    Direction = direction,
                    Progress = progress,
                    Position = state.CurrentPosition
                });
            }
        }
    }

    private void Element_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isEnabled || sender is not UIElement element)
            return;

        var pointer = e.GetCurrentPoint(element);
        var pointerId = pointer.PointerId;

        if (_activeGestures.TryGetValue(pointerId, out var state))
        {
            state.CurrentPosition = pointer.Position;
            state.EndTime = DateTime.UtcNow;

            EvaluateAndRaiseFlick(state);
            _activeGestures.Remove(pointerId);
        }
    }

    private void Element_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        var pointer = e.GetCurrentPoint(null);
        _activeGestures.Remove(pointer.PointerId);

        FlickCanceled?.Invoke(this, new FlickCanceledEventArgs
        {
            Element = sender as UIElement
        });
    }

    #endregion

    #region Manipulation Event Handlers

    private void Element_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
    {
        if (!_isEnabled)
            return;

        // Reset any existing tracking for this manipulation
    }

    private void Element_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (!_isEnabled || sender is not UIElement element)
            return;

        // Provide real-time feedback during manipulation
        var cumulativeX = e.Cumulative.Translation.X;
        var cumulativeY = e.Cumulative.Translation.Y;
        var delta = new Vector2((float)cumulativeX, (float)cumulativeY);

        if (Math.Abs(delta.X) > 10)
        {
            var direction = GetFlickDirection(delta);
            var progress = Math.Min(1.0, Math.Abs(delta.X) / MinFlickDistance);

            FlickProgress?.Invoke(this, new FlickProgressEventArgs
            {
                Element = element,
                Direction = direction,
                Progress = progress,
                Position = e.Position
            });
        }
    }

    private void Element_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        if (!_isEnabled || sender is not UIElement element)
            return;

        var velocityX = e.Velocities.Linear.X;
        var velocityY = e.Velocities.Linear.Y;
        var cumulativeX = e.Cumulative.Translation.X;
        var cumulativeY = e.Cumulative.Translation.Y;

        var delta = new Vector2((float)cumulativeX, (float)cumulativeY);
        var velocity = new Vector2((float)(velocityX * 1000), (float)(velocityY * 1000)); // Convert to pixels/second

        var flickResult = EvaluateFlick(delta, velocity, false);

        if (flickResult.IsFlick)
        {
            RaiseFlickDetected(element, flickResult.Direction, velocity.Length(), e.Position);
        }
        else
        {
            FlickCanceled?.Invoke(this, new FlickCanceledEventArgs
            {
                Element = element
            });
        }
    }

    #endregion

    #region Flick Evaluation

    private void EvaluateAndRaiseFlick(GestureTrackingState state)
    {
        var deltaX = state.CurrentPosition.X - state.StartPosition.X;
        var deltaY = state.CurrentPosition.Y - state.StartPosition.Y;
        var delta = new Vector2((float)deltaX, (float)deltaY);

        var duration = (state.EndTime - state.StartTime).TotalMilliseconds;
        var velocity = delta.Length() / (float)(duration / 1000.0);
        var velocityVector = new Vector2(
            (float)(deltaX / (duration / 1000.0)),
            (float)(deltaY / (duration / 1000.0)));

        var flickResult = EvaluateFlick(delta, velocityVector, state.IsFromEdge);

        if (flickResult.IsFlick)
        {
            RaiseFlickDetected(state.Element, flickResult.Direction, velocity, state.CurrentPosition);
        }
        else
        {
            FlickCanceled?.Invoke(this, new FlickCanceledEventArgs
            {
                Element = state.Element
            });
        }
    }

    private FlickEvaluationResult EvaluateFlick(Vector2 delta, Vector2 velocity, bool isFromEdge)
    {
        var distance = delta.Length();
        var speed = velocity.Length();

        // Check minimum thresholds
        // Allow slightly shorter distances for edge swipes
        var requiredDistance = isFromEdge ? MinFlickDistance * 0.7 : MinFlickDistance;

        if (distance < requiredDistance || speed < MinFlickVelocity)
        {
            return new FlickEvaluationResult { IsFlick = false };
        }

        // Determine primary direction
        var direction = GetFlickDirection(delta);

        // Check if the gesture is predominantly horizontal or vertical
        var angle = Math.Atan2(Math.Abs(delta.Y), Math.Abs(delta.X)) * 180 / Math.PI;

        // For right/left flicks, ensure the angle is within tolerance
        if (direction == FlickDirection.Right || direction == FlickDirection.Left)
        {
            if (angle > DirectionTolerance)
            {
                return new FlickEvaluationResult { IsFlick = false };
            }
        }
        else // Up/Down flicks
        {
            if ((90 - angle) > DirectionTolerance)
            {
                return new FlickEvaluationResult { IsFlick = false };
            }
        }

        return new FlickEvaluationResult
        {
            IsFlick = true,
            Direction = direction,
            Velocity = speed
        };
    }

    private static FlickDirection GetFlickDirection(Vector2 delta)
    {
        // Determine primary direction based on the larger component
        if (Math.Abs(delta.X) > Math.Abs(delta.Y))
        {
            return delta.X > 0 ? FlickDirection.Right : FlickDirection.Left;
        }
        return delta.Y > 0 ? FlickDirection.Down : FlickDirection.Up;
    }

    private bool IsFromLeftEdge(Point position, UIElement element)
    {
        return position.X < EdgeSwipeThreshold;
    }

    private void RaiseFlickDetected(UIElement? element, FlickDirection direction, float velocity, Point position)
    {
        var args = new FlickDetectedEventArgs
        {
            Element = element,
            Direction = direction,
            Velocity = velocity,
            Position = position,
            Timestamp = DateTime.UtcNow
        };

        FlickDetected?.Invoke(this, args);

        // Raise direction-specific events
        switch (direction)
        {
            case FlickDirection.Right:
                RightFlickDetected?.Invoke(this, args);
                break;
            case FlickDirection.Left:
                LeftFlickDetected?.Invoke(this, args);
                break;
            case FlickDirection.Up:
                UpFlickDetected?.Invoke(this, args);
                break;
            case FlickDirection.Down:
                DownFlickDetected?.Invoke(this, args);
                break;
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when any flick gesture is detected.
    /// </summary>
    public event EventHandler<FlickDetectedEventArgs>? FlickDetected;

    /// <summary>
    /// Raised specifically when a right flick gesture is detected.
    /// Use this to trigger dropdown navigation menus.
    /// </summary>
    public event EventHandler<FlickDetectedEventArgs>? RightFlickDetected;

    /// <summary>
    /// Raised when a left flick gesture is detected.
    /// </summary>
    public event EventHandler<FlickDetectedEventArgs>? LeftFlickDetected;

    /// <summary>
    /// Raised when an up flick gesture is detected.
    /// </summary>
    public event EventHandler<FlickDetectedEventArgs>? UpFlickDetected;

    /// <summary>
    /// Raised when a down flick gesture is detected.
    /// </summary>
    public event EventHandler<FlickDetectedEventArgs>? DownFlickDetected;

    /// <summary>
    /// Raised during a potential flick to provide progress feedback.
    /// </summary>
    public event EventHandler<FlickProgressEventArgs>? FlickProgress;

    /// <summary>
    /// Raised when a flick gesture is canceled or does not meet thresholds.
    /// </summary>
    public event EventHandler<FlickCanceledEventArgs>? FlickCanceled;

    #endregion

    #region Nested Types

    private sealed class GestureTrackingState
    {
        public uint PointerId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public Point StartPosition { get; set; }
        public Point CurrentPosition { get; set; }
        public UIElement? Element { get; set; }
        public bool IsFromEdge { get; set; }
    }

    private readonly struct FlickEvaluationResult
    {
        public bool IsFlick { get; init; }
        public FlickDirection Direction { get; init; }
        public float Velocity { get; init; }
    }

    #endregion
}

/// <summary>
/// Direction of a flick gesture.
/// </summary>
public enum FlickDirection
{
    /// <summary>
    /// No direction (invalid flick).
    /// </summary>
    None,

    /// <summary>
    /// Flick to the right. Typically opens navigation or context menus.
    /// </summary>
    Right,

    /// <summary>
    /// Flick to the left. Typically closes menus or navigates back.
    /// </summary>
    Left,

    /// <summary>
    /// Flick upward.
    /// </summary>
    Up,

    /// <summary>
    /// Flick downward.
    /// </summary>
    Down
}

/// <summary>
/// Event arguments for when a flick gesture is detected.
/// </summary>
public sealed class FlickDetectedEventArgs : EventArgs
{
    /// <summary>
    /// The UI element that received the flick.
    /// </summary>
    public UIElement? Element { get; init; }

    /// <summary>
    /// The direction of the flick.
    /// </summary>
    public FlickDirection Direction { get; init; }

    /// <summary>
    /// The velocity of the flick in pixels per second.
    /// </summary>
    public float Velocity { get; init; }

    /// <summary>
    /// The position where the flick ended.
    /// </summary>
    public Point Position { get; init; }

    /// <summary>
    /// When the flick was detected.
    /// </summary>
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Event arguments for flick progress during a gesture.
/// </summary>
public sealed class FlickProgressEventArgs : EventArgs
{
    /// <summary>
    /// The UI element receiving the gesture.
    /// </summary>
    public UIElement? Element { get; init; }

    /// <summary>
    /// The current direction of the gesture.
    /// </summary>
    public FlickDirection Direction { get; init; }

    /// <summary>
    /// Progress from 0.0 to 1.0 indicating how close the gesture is to being a flick.
    /// </summary>
    public double Progress { get; init; }

    /// <summary>
    /// Current position of the gesture.
    /// </summary>
    public Point Position { get; init; }
}

/// <summary>
/// Event arguments for when a flick gesture is canceled.
/// </summary>
public sealed class FlickCanceledEventArgs : EventArgs
{
    /// <summary>
    /// The UI element where the gesture was canceled.
    /// </summary>
    public UIElement? Element { get; init; }
}
