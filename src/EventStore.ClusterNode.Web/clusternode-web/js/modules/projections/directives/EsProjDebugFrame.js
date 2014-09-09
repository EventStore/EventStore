define(['./_module'], function (app) {

    'use strict';

    function destroy () {
		var elem = document.getElementById('script-placeholder');

		if(elem) {
			elem.parentNode.removeChild(elem);
		}
    }

    // http://stackoverflow.com/questions/6248666/how-to-generate-short-uid-like-ax4j9z-in-js
	function generateUIDNotMoreThan1million() {
		return ('0000' + (Math.random()*Math.pow(36,4) << 0).toString(36)).substr(-4);
	}

    return app.directive('esProjDebugFrame', ['$rootScope', function ($rootScope) {
		return {
			restrict: 'A',
			scope: {
				esLocation: '='
			},
			link: function (scope, elem) {
				var iframeHtml = '<div id="text"></div>' +
'<script src="BASE_URI/web/es/js/projections/v8/Prelude/Modules.js"></script>' +
'<script src="BASE_URI/web/es/js/projections/v8/Prelude/Projections.js"></script>' +
'<script src="BASE_URI/web/es/js/projections/es.projections.environment.js"></script>' +
'<script src="BASE_URI/web/es/js/projections/v8/Prelude/1Prelude.js"></script>' +
'<script>window.processor = $initialize_hosted_projections(); processor.set_debugging();</script>' +
'<script src="LOCATION/query?UID"></script>';

				iframeHtml = iframeHtml.replace(/BASE_URI/g, $rootScope.baseUrl);
				iframeHtml = iframeHtml.replace(/LOCATION/g, decodeURIComponent(scope.esLocation));
				iframeHtml = iframeHtml.replace(/UID/g, generateUIDNotMoreThan1million());

				scope.$on('load-scripts', function () {

					destroy();

					var iframe = document.createElement('iframe'),
						doc;
		            
		            iframe.setAttribute('frameborder', '0');
		            iframe.id = 'script-placeholder';
		            elem[0].appendChild(iframe);	

		            doc = iframe.document;
		            
		            if (iframe.contentDocument) { 
						doc = iframe.contentDocument;
		            } else if (iframe.contentWindow) {
						doc = iframe.contentWindow.document;	
		            } 
		 
		            doc.open();
		            doc.writeln(iframeHtml);
		            doc.close();
				});
			}
		};
	}]);
});