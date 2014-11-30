using System;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Messages;
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
        private static readonly ICodec[] SupportedCodecs = new ICodec[] { Codec.Json, Codec.Xml, Codec.ApplicationXml, Codec.Text };
        private readonly IOptions options;
        public InfoController(IOptions options)
        {
            this.options = options;
        }

        public void Subscribe(IHttpService service)
        {
            Ensure.NotNull(service, "service");
            service.RegisterAction(new ControllerAction("/info", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs), OnGetInfo);
            service.RegisterAction(new ControllerAction("/info/options", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs), OnGetOptions);
        }

        private void OnGetInfo(HttpEntityManager entity, UriTemplateMatch match)
        {
            entity.ReplyTextContent(Codec.Json.To(new
            {
                ESVersion = VersionInfo.Version
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
                entity.ReplyTextContent(Codec.Json.To(Filter(GetOptionsInfo(options), new[] { "CertificatePassword" })),
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
                var argumentDescriptionAttribute = property.HasAttr<ArgDescriptionAttribute>() == true ? property.Attr<ArgDescriptionAttribute>() : null;
                var configFileOptionValue = property.GetValue(options, null);
                string[] possibleValues = null;
                if (property.PropertyType.IsEnum)
                {
                    possibleValues = property.PropertyType.GetEnumNames();
                }
                else if (property.PropertyType.IsArray)
                {
                    var array = configFileOptionValue as Array;
                    var configFileOptionValueAsString = String.Empty;
                    for (int i = 0; i < array.Length; i++)
                    {
                        configFileOptionValueAsString += array.GetValue(i).ToString();
                    }
                    configFileOptionValue = configFileOptionValueAsString;
                }
                optionsToSendToClient.Add(new OptionStructure
                {
                    Name = property.Name,
                    Description = argumentDescriptionAttribute.Description,
                    Group = argumentDescriptionAttribute.Group,
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