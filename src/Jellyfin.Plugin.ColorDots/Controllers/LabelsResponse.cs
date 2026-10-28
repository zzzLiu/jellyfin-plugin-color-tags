using System.Collections.Generic;

namespace Jellyfin.Plugin.ColorDots.Controllers;

public sealed record LabelsResponse(IReadOnlyList<LabelDto> Labels);
