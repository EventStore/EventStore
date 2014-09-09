define(['./_module'], function (app) {

    'use strict';

    return app.controller('StreamsItemAclCtrl', [
		'$scope', '$state', '$stateParams', 'StreamsService', 'MessageService',
		function ($scope, $state, $stateParams, streamsService, msg) {
			
			var metadata, err = function () {
				alert('could not update alc');
			};

			$scope.streamId = $stateParams.streamId;
			
			streamsService.getAcl($scope.streamId)
			.success(function (data) {
				if(!data) {
					return;
				}

				metadata = data;
				if(data.$acl) {
					$scope.reader = data.$acl.$r;
					$scope.writer = data.$acl.$w;
					$scope.deleter = data.$acl.$d;
					$scope.metareader = data.$acl.$mr;
					$scope.metawriter = data.$acl.$mw;
				}
			})
			.error(function () {
				msg.error('could not load metadata for stream: ' + $scope.streamId);

				$state.go('^.events');
			});

			$scope.updateAcl = function () {
				var post = metadata || {};
				post.$acl = {
					$r: $scope.reader,
					$w: $scope.writer,
					$d: $scope.deleter,
					$mr: $scope.metareader,
					$mw: $scope.metawriter
				};
				//console.dir(post);
				streamsService.updateAcl($scope.streamId, post)
					.then(function () {
						msg.info('acl updated');
						$state.go('^.events');
					}, err);
			};
		}
	]);
});

