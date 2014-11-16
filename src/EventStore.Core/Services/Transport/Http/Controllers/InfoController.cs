using System;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using EventStore.Common.Options;
using System.Collections.Generic;
using EventStore.Rags;
using System.Linq;

namespace EventStore.Core.Services.Transport.Http.Controllers
{
    public class InfoController : IHttpController
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<InfoController>();
        private static readonly ICodec[] SupportedCodecs = { Codec.Json, Codec.Xml, Codec.ApplicationXml, Codec.Text };

        private readonly IOptions _options;
        private readonly ProjectionType _projectionType;

        public InfoController(IOptions options, ProjectionType projectionType)
        {
            _options = options;
            _projectionType = projectionType;
        }

        public void Subscribe(IHttpService service)
        {
            Ensure.NotNull(service, "service");
            service.RegisterAction(new ControllerAction("/info", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs), OnGetInfo);
            service.RegisterAction(new ControllerAction("/info/options", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs), OnGetOptions);
            service.RegisterAction(new ControllerAction("/info/options", HttpMethod.Post, Codec.NoCodecs, SupportedCodecs), OnUpdateOptions);
        }

        private void OnGetInfo(HttpEntityManager entity, UriTemplateMatch match)
        {
            entity.ReplyTextContent(Codec.Json.To(new
            {
                                        ESVersion = VersionInfo.Version,
					ProjectionsMode = _projectionType
            }),
             HttpStatusCode.OK,
             "OK",
             entity.ResponseCodec.ContentType,
             null,
             e => Log.ErrorException(e, "Error while writing http response (info)"));
        }

        private void OnGetOptions(HttpEntityManager entity, UriTemplateMatch match)
        {
            if (entity.User != null && entity.User.IsInRole(SystemRoles.Admins))
            {
                entity.ReplyTextContent(Codec.Json.To(Filter(GetOptionsInfo(_options), new[] { "CertificatePassword" })),
                                        HttpStatusCode.OK,
                                        "OK",
                                        entity.ResponseCodec.ContentType,
                                        null,
                                        e => Log.ErrorException(e, "error while writing http response (options)"));
            }
            else
            {
                entity.ReplyStatus(HttpStatusCode.Unauthorized, "Unauthorized", LogReplyError);
            }
        }

        private void LogReplyError(Exception exc)
        {
            Log.Debug("Error while replying (info controller): {0}.", exc.Message);
        }

        private void OnUpdateOptions(HttpEntityManager entity, UriTemplateMatch match)
        {
            //if (entity.User != null && entity.User.IsInRole(SystemRoles.Admins))
            //{
                entity.ReadTextRequestAsync(
                                (man, body) =>
                                {
                                    OptionSource[] optionsToUpdate = null;
                                    try
                                    {
                                        optionsToUpdate = Json.ParseJson<OptionSource[]>(body);
                                        var updatedOptions = EventStoreOptions.Update(optionsToUpdate);
                                        man.ReplyTextContent(Codec.Json.To(updatedOptions.ToArray()),
                                            HttpStatusCode.OK,
                                            "OK",
                                            entity.ResponseCodec.ContentType,
                                            null,
                                            e => Log.ErrorException(e, "error while writing http response (options)"));
                                    }
                                    catch (Exception ex)
                                    {
                                        SendBadRequest(man, ex.Message);
                                    }
                                },
                                e => Log.Debug("Error while reading request (POST entry): {0}.", e.Message));
            //}
            //else
            //{
            //    entity.ReplyStatus(HttpStatusCode.Unauthorized, "Unauthorized", LogReplyError);
            //}
        }

        protected RequestParams SendBadRequest(HttpEntityManager httpEntityManager, string reason)
        {
            httpEntityManager.ReplyTextContent(reason,
                                    HttpStatusCode.BadRequest,
                                    reason,
                                    httpEntityManager.ResponseCodec.ContentType,
                                    null,
                                    e => Log.Debug("Error while closing http connection (bad request): {0}.", e.Message));
            return new RequestParams(done: true);
        }

        protected RequestParams SendOk(HttpEntityManager httpEntityManager)
        {
            httpEntityManager.ReplyStatus(HttpStatusCode.OK,
                                          "OK",
                                          e => Log.Debug("Error while closing http connection (ok): {0}.", e.Message));
            return new RequestParams(done: true);
        }

        public class OptionStructure
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string Group { get; set; }
            public string Value { get; set; }
            public string[] PossibleValues { get; set; }
        }

        public OptionStructure[] GetOptionsInfo(IOptions options)
        {
            var optionsToSendToClient = new List<OptionStructure>();
            foreach (var property in options.GetType().GetProperties())
            {
                var argumentDescriptionAttribute = property.HasAttr<ArgDescriptionAttribute>() ? property.Attr<ArgDescriptionAttribute>() : null;
                var configFileOptionValue = property.GetValue(options, null);
                string[] possibleValues = null;
                if (property.PropertyType.IsEnum)
                {
                    possibleValues = property.PropertyType.GetEnumNames();
                }
                else if (property.PropertyType.IsArray)
                {
                    var array = configFileOptionValue as Array;
                    if (array == null)
                        continue;
                    var configFileOptionValueAsString = String.Empty;
                    for (var i = 0; i < array.Length; i++)
                    {
                        configFileOptionValueAsString += array.GetValue(i).ToString();
                    }
                    configFileOptionValue = configFileOptionValueAsString;
                }
                optionsToSendToClient.Add(new OptionStructure
                {
                    Name = property.Name,
                    Description = argumentDescriptionAttribute == null ? "" : argumentDescriptionAttribute.Description,
                    Group = argumentDescriptionAttribute == null ? "" : argumentDescriptionAttribute.Group,
                    Value = configFileOptionValue.ToString(),
                    PossibleValues = possibleValues
                });
            }
            return optionsToSendToClient.ToArray();
        }
        public OptionStructure[] Filter(OptionStructure[] optionsToBeFiltered, params string[] namesOfValuesToExclude)
        {
            return optionsToBeFiltered.Select(x =>
                    new OptionStructure
                    {
                        Name = x.Name,
                        Description = x.Description,
                        Group = x.Group,
                        PossibleValues = x.PossibleValues,
                        Value = namesOfValuesToExclude.Contains(y => y.Equals(x.Name, StringComparison.OrdinalIgnoreCase)) ? String.Empty : x.Value
                    }).ToArray();
        }
    }
}