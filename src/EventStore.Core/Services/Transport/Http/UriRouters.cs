﻿using System;
using System.Collections.Generic;
using EventStore.Common.Utils;
using EventStore.Transport.Http.EntityManagement;
using EventStore.UriTemplate;

namespace EventStore.Core.Services.Transport.Http {
	public interface IUriRouter {
		void RegisterAction(ControllerAction action, Func<HttpEntityManager, UriTemplateMatch, RequestParams> handler);
		List<UriToActionMatch> GetAllUriMatches(Uri uri);
	}

	public class TrieUriRouter : IUriRouter {
		private const string Placeholder = "{}";
		private const string GreedyPlaceholder = "{*}";

		private readonly RouterNode _root = new RouterNode();

		public void RegisterAction(ControllerAction action,
			Func<HttpEntityManager, UriTemplateMatch, RequestParams> handler) {
			Ensure.NotNull(action, "action");
			Ensure.NotNull(handler, "handler");

			var segments = new Uri("http://fake" + action.UriTemplate, UriKind.Absolute).Segments;
			RouterNode node = _root;
			foreach (var segm in segments) {
				var segment = Uri.UnescapeDataString(segm);
				string path = segment.StartsWith("{*") ? GreedyPlaceholder
					: segment.StartsWith("{") ? Placeholder
					: segment;

				RouterNode child;
				if (!node.Children.TryGetValue(path, out child)) {
					child = new RouterNode();
					node.Children.Add(path, child);
				}

				node = child;
			}

			if (node.LeafRoutes.Contains(x => x.Action.Equals(action)))
				throw new ArgumentException("Duplicate route.");
			node.LeafRoutes.Add(new HttpRoute(action, handler));
		}

		public List<UriToActionMatch> GetAllUriMatches(Uri uri) {
			var matches = new List<UriToActionMatch>();
			var baseAddress = new UriBuilder(uri.Scheme, uri.Host, uri.Port).Uri;


			var segments = new string[uri.Segments.Length];
			for (int i = 0; i < uri.Segments.Length; i++) {
				segments[i] = Uri.UnescapeDataString(uri.Segments[i]);
			}

			GetAllUriMatches(_root, baseAddress, uri, segments, 0, matches);

			return matches;
		}

		private void GetAllUriMatches(RouterNode node, Uri baseAddress, Uri uri, string[] segments, int index,
			List<UriToActionMatch> matches) {
			RouterNode child;

			if (index == segments.Length) {
				// /stats/ should match /stats/{*greedyStatsPath}
				if (uri.OriginalString.EndsWith("/") && node.Children.TryGetValue(GreedyPlaceholder, out child))
					AddMatchingRoutes(child.LeafRoutes, baseAddress, uri, matches);

				AddMatchingRoutes(node.LeafRoutes, baseAddress, uri, matches);
				return;
			}

			if (node.Children.TryGetValue(GreedyPlaceholder, out child))
				GetAllUriMatches(child, baseAddress, uri, segments, segments.Length, matches);
			if (node.Children.TryGetValue(Placeholder, out child))
				GetAllUriMatches(child, baseAddress, uri, segments, index + 1, matches);
			if (node.Children.TryGetValue(segments[index], out child))
				GetAllUriMatches(child, baseAddress, uri, segments, index + 1, matches);
		}

		private static void AddMatchingRoutes(IList<HttpRoute> routes, Uri baseAddress, Uri uri,
			List<UriToActionMatch> matches) {
			for (int i = 0; i < routes.Count; ++i) {
				var route = routes[i];
				var match = route.UriTemplate.Match(baseAddress, uri);
				if (match != null)
					matches.Add(new UriToActionMatch(match, route.Action, route.Handler));
			}
		}

		private class RouterNode {
			public readonly Dictionary<string, RouterNode> Children = new Dictionary<string, RouterNode>();
			public readonly List<HttpRoute> LeafRoutes = new List<HttpRoute>();
		}
	}

	public class NaiveUriRouter : IUriRouter {
		private readonly List<HttpRoute> _actions = new List<HttpRoute>();

		public void RegisterAction(ControllerAction action,
			Func<HttpEntityManager, UriTemplateMatch, RequestParams> handler) {
			if (_actions.Contains(x => x.Action.Equals(action)))
				throw new ArgumentException("Duplicate route.");
			_actions.Add(new HttpRoute(action, handler));
		}

		public List<UriToActionMatch> GetAllUriMatches(Uri uri) {
			var matches = new List<UriToActionMatch>();
			var baseAddress = new UriBuilder(uri.Scheme, uri.Host, uri.Port).Uri;
			for (int i = 0; i < _actions.Count; ++i) {
				var route = _actions[i];
				var match = route.UriTemplate.Match(baseAddress, uri);
				if (match != null)
					matches.Add(new UriToActionMatch(match, route.Action, route.Handler));
			}

			return matches;
		}
	}

	internal class HttpRoute {
		public readonly ControllerAction Action;
		public readonly Func<HttpEntityManager, UriTemplateMatch, RequestParams> Handler;
		public readonly UriTemplate.UriTemplate UriTemplate;

		public HttpRoute(ControllerAction action, Func<HttpEntityManager, UriTemplateMatch, RequestParams> handler) {
			Action = action;
			Handler = handler;
			UriTemplate = new UriTemplate.UriTemplate(action.UriTemplate);
		}
	}
}
