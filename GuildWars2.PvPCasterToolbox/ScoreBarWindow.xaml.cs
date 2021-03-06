﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GuildWars2.Overlay.Controls;
using GuildWars2.PvPCasterToolbox.Controls;
using ImageMagick;
using WindowsPoint = System.Windows.Point;

namespace GuildWars2.PvPCasterToolbox
{
    /// <summary>
    /// Interaction logic for ScoreBarWindow.xaml
    /// </summary>
    public partial class ScoreBarWindow : ClickThroughTransparentWindow
    {
        private static readonly WindowsPoint GradientOpacityLeftPoint = new WindowsPoint(0, 0);
        private static readonly WindowsPoint GradientOpacityRightPoint = new WindowsPoint(1, 0);

        private readonly ScoreBarAnimator scoreBarAnimator;

        private WriteableBitmap scoreBarBitmap;
        private IMagickImage scoreBarImage;
        private ImageModulationParameters modulationParameters = new ImageModulationParameters(0, 0, 0);

        public double ScoreBarFill
        {
            get => this.scoreBarAnimator.CurrentValue;
            set => this.scoreBarAnimator.BeginAnimateScore(value, this);
        }

        public ImageModulationParameters ScoreBarModulation
        {
            get => this.modulationParameters;
            set
            {
                if (this.modulationParameters != value)
                {
                    this.modulationParameters = value;

                    using (IMagickImage clone = this.scoreBarImage.Clone())
                    {
                        clone.Modulate(new Percentage(modulationParameters.BrightnessPercentage),
                                       new Percentage(modulationParameters.SaturationPercentage),
                                       new Percentage(modulationParameters.HuePercentage));
                        this.scoreBarBitmap.WriteMagickImage(clone);
                    }
                }
            }
        }

        public ScoreBarWindow(string backgroundBarImagePath, string boostBarImagePath, string scoreBarImagePath,
                              bool isFlipped = false, bool overlayMode = true, RectangleF? position = null)
        {
            InitializeComponent();

            this.BoostBar_GradientOpacityMask.StartPoint =
            this.ScoreBar_GradientOpacityMask.StartPoint = isFlipped ? GradientOpacityRightPoint : GradientOpacityLeftPoint;
            this.BoostBar_GradientOpacityMask.EndPoint =
            this.ScoreBar_GradientOpacityMask.EndPoint = isFlipped ? GradientOpacityLeftPoint : GradientOpacityRightPoint;

            this.BackgroundBar.Source = new BitmapImage(new Uri(Path.GetFullPath(backgroundBarImagePath)));
            this.BoostBar.Source = new BitmapImage(new Uri(Path.GetFullPath(boostBarImagePath)));

            this.scoreBarImage = new MagickImage(Path.GetFullPath(scoreBarImagePath));
            this.scoreBarBitmap = new WriteableBitmap(this.scoreBarImage.Width, this.scoreBarImage.Height,
                                                      this.scoreBarImage.Density.X, this.scoreBarImage.Density.Y,
                                                      PixelFormats.Bgra32, null);
            this.scoreBarBitmap.WriteMagickImage(this.scoreBarImage);
            this.ScoreBar.Source = this.scoreBarBitmap;

            this.scoreBarAnimator = new ScoreBarAnimator(nameof(this.BoostBar_GradientOpacityMask_TransparentStop),
                                                         nameof(this.BoostBar_GradientOpacityMask_BlackStop),
                                                         nameof(this.ScoreBar_GradientOpacityMask_TransparentStop),
                                                         nameof(this.ScoreBar_GradientOpacityMask_BlackStop));

            this.AllowsTransparency = 
            this.Topmost = overlayMode;

            this.GreenScreen.Visibility = overlayMode ? Visibility.Hidden : Visibility.Visible;

            if (position != null)
            {
                this.SetWindowRect(position.Value);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.LeftShift)
            {
                this.WindowUI.Visibility = (this.WindowUI.IsVisible ? Visibility.Hidden : Visibility.Visible);
            }

            if (e.Key == Key.LeftCtrl)
            {
                this.IsClickThroughTransparent = !this.IsClickThroughTransparent;
            }

            base.OnKeyDown(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            this.scoreBarImage.Dispose();

            base.OnClosing(e);
        }
    }
}
