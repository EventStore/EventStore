using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Net;

namespace EventStore.UriTemplate {
	public class UriTemplateMatch {
		private Uri _baseUri;
		private NameValueCollection _boundVariables;
		private object _data;
		private NameValueCollection _queryParameters;
		private Collection<string> _relativePathSegments;
		private Uri _requestUri;
		private UriTemplate _template;
		private Collection<string> _wildcardPathSegments;
		private int _wildcardSegmentsStartOffset = -1;
		private Uri _originalBaseUri;
		private HttpRequestMessageProperty _requestProp;

		public UriTemplateMatch() {
		}

		public Uri BaseUri   // the base address, untouched
		{
			get {
				if (_baseUri == null && _originalBaseUri != null) {
					_baseUri = UriTemplate.RewriteUri(_originalBaseUri, _requestProp.Headers[HttpRequestHeader.Host]);
				}
				return _baseUri;
			}
			set {
				_baseUri = value;
				_originalBaseUri = null;
				_requestProp = null;
			}
		}
		public NameValueCollection BoundVariables // result of TryLookup, values are decoded
		{
			get {
				if (_boundVariables == null) {
					_boundVariables = new NameValueCollection();
				}
				return _boundVariables;
			}
		}
		public object Data {
			get {
				return _data;
			}
			set {
				_data = value;
			}
		}
		public NameValueCollection QueryParameters  // the result of UrlUtility.ParseQueryString (keys and values are decoded)
		{
			get {
				if (_queryParameters == null) {
					PopulateQueryParameters();
				}
				return _queryParameters;
			}
		}
		public Collection<string> RelativePathSegments  // entire Path (after the base address), decoded
		{
			get {
				if (_relativePathSegments == null) {
					_relativePathSegments = new Collection<string>();
				}
				return _relativePathSegments;
			}
		}
		public Uri RequestUri  // uri on the wire, untouched
		{
			get {
				return _requestUri;
			}
			set {
				_requestUri = value;
			}
		}
		public UriTemplate Template // which one got matched
		{
			get {
				return _template;
			}
			set {
				_template = value;
			}
		}
		public Collection<string> WildcardPathSegments  // just the Path part matched by "*", decoded
		{
			get {
				if (_wildcardPathSegments == null) {
					PopulateWildcardSegments();
				}
				return _wildcardPathSegments;
			}
		}

		internal void SetQueryParameters(NameValueCollection queryParameters) {
			_queryParameters = new NameValueCollection(queryParameters);
		}
		internal void SetRelativePathSegments(Collection<string> segments) {
			Ensure.NotNull(segments, "segments != null");
			_relativePathSegments = segments;
		}
		internal void SetWildcardPathSegmentsStart(int startOffset) {
			Ensure.Nonnegative(startOffset, "startOffset >= 0");
			_wildcardSegmentsStartOffset = startOffset;
		}

		internal void SetBaseUri(Uri originalBaseUri, HttpRequestMessageProperty requestProp) {
			_baseUri = null;
			_originalBaseUri = originalBaseUri;
			_requestProp = requestProp;
		}

		private void PopulateQueryParameters() {
			if (_requestUri != null) {
				_queryParameters = UriTemplateHelpers.ParseQueryString(_requestUri.Query);
			} else {
				_queryParameters = new NameValueCollection();
			}
		}

		private void PopulateWildcardSegments() {
			if (_wildcardSegmentsStartOffset != -1) {
				_wildcardPathSegments = new Collection<string>();
				for (var i = _wildcardSegmentsStartOffset; i < RelativePathSegments.Count; ++i) {
					_wildcardPathSegments.Add(RelativePathSegments[i]);
				}
			} else {
				_wildcardPathSegments = new Collection<string>();
			}
		}
	}
}
