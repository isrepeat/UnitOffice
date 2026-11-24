using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections {
    public sealed partial class CandidatesSection : UserControl {
        public CandidatesSectionViewModel? ViewModel {
            get => (CandidatesSectionViewModel?)this.GetValue(ViewModelProperty);
            set => this.SetValue(ViewModelProperty, value);
        }
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            nameof(ViewModel),
            typeof(CandidatesSectionViewModel),
            typeof(CandidatesSection),
            new PropertyMetadata(null)
        );

        public CandidatesSection() {
            this.InitializeComponent();
        }
    }
}