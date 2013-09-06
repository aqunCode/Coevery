using System;
using System.Linq;
using JetBrains.Annotations;
using Orchard.ContentManagement.MetaData;
using Orchard.Data;
using Orchard.Localization;
using Orchard.ContentManagement;
using Orchard.ContentManagement.Handlers;
using Orchard.Security;
using Orchard.Services;
using Coevery.Core.Models;

namespace Coevery.Core.Handlers {
    [UsedImplicitly]
    public class CoeveryCommonPartHandler : ContentHandler {
        private readonly IClock _clock;
        private readonly IAuthenticationService _authenticationService;
        private readonly IContentManager _contentManager;
        private readonly IContentDefinitionManager _contentDefinitionManager;

        public CoeveryCommonPartHandler(
            IRepository<CoeveryCommonPartRecord> commonRepository,
            IRepository<CoeveryCommonPartVersionRecord> commonVersionRepository,
            IClock clock,
            IAuthenticationService authenticationService,
            IContentManager contentManager,
            IContentDefinitionManager contentDefinitionManager) {

            _clock = clock;
            _authenticationService = authenticationService;
            _contentManager = contentManager;
            _contentDefinitionManager = contentDefinitionManager;
            T = NullLocalizer.Instance;

            Filters.Add(StorageFilter.For(commonRepository));
            Filters.Add(StorageFilter.For(commonVersionRepository));

            OnActivated<CoeveryCommonPart>(PropertySetHandlers);
            OnCreated<CoeveryCommonPart>(AssignCreatingOwner);
            OnCreated<CoeveryCommonPart>(AssignCreatingDates);

            OnLoading<CoeveryCommonPart>((context, part) => LazyLoadHandlers(part));
            OnVersioning<CoeveryCommonPart>((context, part, newVersionPart) => LazyLoadHandlers(newVersionPart));

            OnUpdated<CoeveryCommonPart>(AssignUpdateUser);
            OnUpdated<CoeveryCommonPart>(AssignUpdateDates);

            //OnVersioning<CoeveryCommonPart>(AssignVersioningDates);

            //OnPublishing<CommonPart>(AssignPublishingDates);

            //OnIndexing<CoeveryCommonPart>((context, commonPart) => {
            //    context.DocumentIndex
            //        .Add("type", commonPart.ContentItem.ContentType).Analyze().Store()
            //        .Add("created", commonPart.CreatedUtc ?? _clock.UtcNow).Store()
            //        //.Add("published", commonPart.PublishedUtc ?? _clock.UtcNow).Store()
            //        .Add("modified", commonPart.ModifiedUtc ?? _clock.UtcNow).Store();

            //    if (commonPart.Owner != null) context.DocumentIndex
            //        .Add("author", commonPart.Owner.UserName).Analyze().Store()
            //        .Add("modifier",commonPart.Modifer.UserName).Analyze().Store();
            //});
        }


        public Localizer T { get; set; }

        protected override void Activating(ActivatingContentContext context) {
            if (ContentTypeWithACommonPart(context.ContentType)) {
                context.Builder.Weld<CoeveryCommonPart>();
                context.Builder.Weld<ContentPart<CoeveryCommonPartVersionRecord>>();
            }
        }

        protected bool ContentTypeWithACommonPart(string typeName) {
            //Note: What about content type handlers which activate "CommonPart" in code?
            var contentTypeDefinition = _contentDefinitionManager.GetTypeDefinition(typeName);

            if (contentTypeDefinition != null)
                return contentTypeDefinition.Parts.Any(part => part.PartDefinition.Name == "CoeveryCommonPart");

            return false;
        }

        protected void AssignCreatingOwner(CreateContentContext context, CoeveryCommonPart part) {
            // and use the current user as Owner
            if (part.Record.OwnerId == 0) {
                part.Owner = _authenticationService.GetAuthenticatedUser();
            }
        }

        protected void AssignCreatingDates(CreateContentContext context, CoeveryCommonPart part) {
            // assign default create/modified dates
            part.Container = context.ContentItem;

            var utcNow = _clock.UtcNow;
            part.CreatedUtc = utcNow;
            part.ModifiedUtc = utcNow;
            part.VersionCreatedUtc = utcNow;
            part.VersionModifiedUtc = utcNow;
        }

        private void AssignUpdateUser(UpdateContentContext context, CoeveryCommonPart part) {
            part.Modifer = _authenticationService.GetAuthenticatedUser();
        }

        private void AssignUpdateDates(UpdateContentContext context, CoeveryCommonPart part) {
            var utcNow = _clock.UtcNow;
            part.ModifiedUtc = utcNow;
            part.VersionModifiedUtc = utcNow;
        }

        //protected void AssignVersioningDates(VersionContentContext context, CoeveryCommonPart existing, CoeveryCommonPart building) {
        //    var utcNow = _clock.UtcNow;

        //     // assign the created date
        //    building.VersionCreatedUtc = utcNow;
        //    // assign modified date for the new version
        //    building.VersionModifiedUtc = utcNow;
        //    // publish date should be null until publish method called
        //    //building.VersionPublishedUtc = null;

        //    // assign the created
        //    building.CreatedUtc = existing.CreatedUtc ?? _clock.UtcNow;
        //    // persist any published dates
        //    //building.PublishedUtc = existing.PublishedUtc;
        //    // assign modified date for the new version
        //    building.ModifiedUtc = _clock.UtcNow;
        //}


        //protected void AssignPublishingDates(PublishContentContext context, CommonPart part) {
        //    var utcNow = _clock.UtcNow;
        //    part.PublishedUtc = utcNow;
        //    part.VersionPublishedUtc = utcNow;
        //}

        protected void LazyLoadHandlers(CoeveryCommonPart part) {
            // add handlers that will load content for id's just-in-time
            part.OwnerField.Loader(() => _contentManager.Get<IUser>(part.Record.OwnerId));
            part.ContainerField.Loader(() => part.Record.Container == null ? null : _contentManager.Get(part.Record.Container.Id));
        }

        protected static void PropertySetHandlers(ActivatedContentContext context, CoeveryCommonPart part) {
            // add handlers that will update records when part properties are set

            part.OwnerField.Setter(user => {
                part.Record.OwnerId = user == null
                                          ? 0
                                          : user.ContentItem.Id;
                return user;
            });

            // Force call to setter if we had already set a value
            if (part.OwnerField.Value != null)
                part.OwnerField.Value = part.OwnerField.Value;

            part.ModifierField.Setter(user => {
                part.Record.ModifierId = user == null
                                             ? 0 : user.ContentItem.Id;
                return user;
            });
            // Force call to setter if we had already set a value
            if (part.ModifierField.Value != null)
                part.ModifierField.Value = part.ModifierField.Value;

            part.ContainerField.Setter(container => {
                part.Record.Container = container == null
                                            ? null : container.ContentItem.Record;
                return container;
            });

            // Force call to setter if we had already set a value
            if (part.ContainerField.Value != null)
                part.ContainerField.Value = part.ContainerField.Value;
        }
    }
}
