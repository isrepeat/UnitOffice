using System.Linq;
using System.Collections.Generic;

namespace PersonalEventsBuilder.Workspace {
    internal static class EventFormLayoutBuilder {
        internal static EventFormLayout Build(ModeWorkspace modeWorkspace) {
            var extensions = modeWorkspace.Extensions;
            var primaryRow = CreateCenterFields(modeWorkspace.CoreFields)
                .Concat(CreateExtensionFields(extensions, FieldPlacement.Inline));

            if (modeWorkspace.CenterFieldOrder is { } order) {
                var positions = order.Select((id, index) => (id, index)).ToDictionary(item => item.id, item => item.index);
                primaryRow = primaryRow.OrderBy(field => positions.GetValueOrDefault(field.Id, int.MaxValue));
            }

            return new EventFormLayout(
                CreateExtensionFields(extensions, FieldPlacement.Left).ToArray(),
                primaryRow.ToArray(),
                CreateExtensionFields(extensions, FieldPlacement.Below).ToArray(),
                CreateExtensionFields(extensions, FieldPlacement.Right).ToArray()
            );
        }

        private static FormField[] CreateCenterFields(IReadOnlyList<CoreFieldState> states) {
            const double coreFieldWidth = 78;
            var byField = states.ToDictionary(state => state.Field);
            return [
                Create(CoreField.Person, "Tr_FieldPerson", FieldKind.Text),
                Create(CoreField.Reporter, "Tr_FieldReporter", FieldKind.Text),
                Create(CoreField.Rank, "Tr_FieldRank", FieldKind.Text),
                Create(CoreField.IncomingNumber, "Tr_FieldIncomingNumber", FieldKind.Text),
                Create(CoreField.IncomingDate, "Tr_FieldIncomingDate", FieldKind.Date),
                Create(CoreField.DocumentType, "Tr_FieldDocument", FieldKind.Choice),
                Create(CoreField.DocumentNumber, "Tr_FieldDocumentNumber", FieldKind.Text),
                Create(CoreField.EventDate, "Tr_FieldEventDate", FieldKind.Date),
            ];

            FormField Create(CoreField field, string titleKey, FieldKind kind) {
                byField.TryGetValue(field, out var state);
                return new FormField(
                    field.ToString(),
                    state?.Title ?? Localization.UiStrings.Get(titleKey),
                    kind,
                    field,
                    coreFieldWidth,
                    state?.IsEnabled ?? true,
                    state?.InitialValue,
                    state?.Options
                );
            }
        }

        private static IEnumerable<FormField> CreateExtensionFields(
            IReadOnlyList<ExtensionField> extensions,
            FieldPlacement placement
        ) => extensions.Where(field => field.Placement == placement
                && (placement != FieldPlacement.Below || field.Anchor.HasValue))
            .Select(field => new FormField(
                field.Id,
                field.Title,
                field.Kind,
                Width: field.Width,
                IsEnabled: field.IsEnabled,
                InitialValue: field.InitialValue,
                Options: field.Options,
                Anchor: placement == FieldPlacement.Below ? field.Anchor : null
            ));
    }
}