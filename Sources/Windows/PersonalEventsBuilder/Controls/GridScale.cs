using System;

using Microsoft.UI.Xaml;

namespace PersonalEventsBuilder.Controls {
    public sealed class GridScale : DependencyObject {
        public double Step {
            get => (double)this.GetValue(StepProperty);
            set {
                if (!double.IsFinite(value) || value < MinimumStep) {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                this.SetValue(StepProperty, value);
            }
        }
        public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
            nameof(Step),
            typeof(double),
            typeof(GridScale),
            new PropertyMetadata(48.0)
        );

        internal const double MinimumStep = 8;
    }
}