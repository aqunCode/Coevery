﻿using Orchard.Models.Driver;
using Orchard.UI.Models;

namespace Orchard.DevTools.Models {
    public class DebugLinkProvider : ContentProvider {
        protected override void GetDisplays(GetDisplaysContext context) {
            context.Displays.Add(new ModelTemplate(new ShowDebugLink { ContentItem = context.ContentItem }) { Position = "10" });
        }
        protected override void GetEditors(GetEditorsContext context) {
            context.Editors.Add(new ModelTemplate(new ShowDebugLink { ContentItem = context.ContentItem }) { Position = "10" });
        }
    }
}
