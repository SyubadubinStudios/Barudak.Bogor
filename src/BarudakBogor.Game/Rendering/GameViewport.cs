using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ThreeNet;

namespace BarudakBogor.Game.Rendering;

/// <summary>Arguments of <see cref="GameViewport.Frame"/>.</summary>
public sealed class FrameEventArgs(float deltaSeconds) : EventArgs
{
    /// <summary>Real seconds since the previous frame.</summary>
    public float DeltaSeconds { get; } = deltaSeconds;
}

/// <summary>
/// The 3D picture inside the window: renders the scene offscreen on the GPU and shows the
/// result as a bitmap, so the HUD composes over it like over any other control.
/// </summary>
/// <remarks>
/// This replaces the engine's <c>ThreeNetView</c> for two reasons. The game pumps its own frames
/// from the display refresh, and it changes <see cref="RenderScale"/> while playing (dynamic
/// resolution), which here only resizes the render target instead of rebuilding the renderer.
/// </remarks>
public sealed class GameViewport : Control
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private Renderer? _renderer;
    private WriteableBitmap? _front;
    private WriteableBitmap? _back;
    private PixelSize _size;
    private TimeSpan _lastFrame;
    private RendererOptions _options = RendererOptions.Default with { BgraOutput = true, VSync = false };
    private double _renderScale = 1.0;
    private byte[] _staging = [];
    private string? _failure;

    public GameViewport()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public Scene? Scene { get; set; }

    /// <summary>Camera node; the scene's active camera when null.</summary>
    public Node? Camera { get; set; }

    /// <summary>Renderer settings. Changing them rebuilds the renderer on the next frame.</summary>
    public RendererOptions RendererOptions
    {
        get => _options;
        set
        {
            if (!_options.Equals(value))
            {
                _options = value;
                DisposeGpuResources();
            }
        }
    }

    /// <summary>Resolution multiplier (1 = the control's native pixels). Cheap to change: the target is resized.</summary>
    public double RenderScale
    {
        get => _renderScale;
        set => _renderScale = Math.Clamp(value, 0.1, 2.0);
    }

    public Renderer? Renderer => _renderer;

    public FrameStats Stats => _renderer?.Stats ?? default;

    /// <summary>Raised before each frame is drawn; step the game here.</summary>
    public event EventHandler<FrameEventArgs>? Frame;

    public event EventHandler<string>? RenderFailed;

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DisposeGpuResources();
    }

    /// <summary>Steps the game and renders one frame.</summary>
    public void RenderFrame()
    {
        if (Scene is null || _failure is not null || Bounds.Width <= 0 || Bounds.Height <= 0 || !IsEffectivelyVisible)
        {
            return;
        }

        double scaling = (TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0) * _renderScale;
        PixelSize size = new(
            Math.Max(1, (int)Math.Round(Bounds.Width * scaling)),
            Math.Max(1, (int)Math.Round(Bounds.Height * scaling)));

        try
        {
            EnsureTarget(size);
            TimeSpan now = _clock.Elapsed;
            float delta = _lastFrame == TimeSpan.Zero ? 1f / 60f : (float)(now - _lastFrame).TotalSeconds;
            _lastFrame = now;
            Frame?.Invoke(this, new FrameEventArgs(delta));
            // The handler may have changed the options (quality switch) or closed the window.
            if (Scene is not { } scene || _renderer is null)
            {
                return;
            }

            _renderer.Render(scene, Camera);
            CopyToBitmap();
        }
        catch (ThreeNetException exception)
        {
            _failure = exception.Message;
            DisposeGpuResources();
            RenderFailed?.Invoke(this, exception.Message);
        }

        InvalidateVisual();
    }

    /// <summary>A copy of the most recent frame, for saving a photo.</summary>
    public unsafe WriteableBitmap? CaptureFrame()
    {
        if (_renderer is null || _size == default)
        {
            return null;
        }

        byte[] pixels = _renderer.ReadPixels();
        WriteableBitmap bitmap = new(_size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using ILockedFramebuffer buffer = bitmap.Lock();
        int rowBytes = _size.Width * 4;
        for (int y = 0; y < _size.Height; y++)
        {
            pixels.AsSpan(y * rowBytes, rowBytes).CopyTo(new Span<byte>((void*)(buffer.Address + (nint)(y * buffer.RowBytes)), rowBytes));
        }

        return bitmap;
    }

    private void EnsureTarget(PixelSize size)
    {
        if (_renderer is not null && _size == size)
        {
            return;
        }

        if (_renderer is null)
        {
            _renderer = Renderer.CreateOffscreen(_options with { Width = size.Width, Height = size.Height, BgraOutput = true });
        }
        else
        {
            _renderer.Resize(size.Width, size.Height);
        }

        _size = size;
        _staging = new byte[_renderer.PixelBufferSize];
        _front?.Dispose();
        _back?.Dispose();
        // 96 dpi: the bitmap is stretched over the control whatever its pixel size.
        _front = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        _back = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
    }

    private unsafe void CopyToBitmap()
    {
        if (_renderer is null || _back is null)
        {
            return;
        }

        // Read into the back bitmap and swap, so the compositor never samples one that is being written.
        using (ILockedFramebuffer buffer = _back.Lock())
        {
            int rowBytes = _size.Width * 4;
            if (buffer.RowBytes == rowBytes)
            {
                _renderer.ReadPixels(new Span<byte>((void*)buffer.Address, rowBytes * _size.Height));
            }
            else
            {
                _renderer.ReadPixels(_staging);
                for (int y = 0; y < _size.Height; y++)
                {
                    _staging.AsSpan(y * rowBytes, rowBytes).CopyTo(new Span<byte>((void*)(buffer.Address + (nint)(y * buffer.RowBytes)), rowBytes));
                }
            }
        }

        (_front, _back) = (_back, _front);
    }

    private void DisposeGpuResources()
    {
        _renderer?.Dispose();
        _renderer = null;
        _front?.Dispose();
        _front = null;
        _back?.Dispose();
        _back = null;
        _size = default;
        _staging = [];
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_front is not null)
        {
            context.DrawImage(_front, new Rect(0, 0, _front.PixelSize.Width, _front.PixelSize.Height), new Rect(Bounds.Size));
        }
        else
        {
            context.FillRectangle(new SolidColorBrush(Color.FromRgb(18, 24, 28)), new Rect(Bounds.Size));
        }
    }
}
