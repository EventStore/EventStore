define(['es-ui'], function (app) {

	'use strict';
	app.value('baseUrl', '');
	
	return app.constant('urls', {
		base: 'http://127.0.0.1:2113',
		guid: '/new-guid',
		stats: '/stats',
		admin: {
			halt: '/admin/halt',
			shutdown: '/admin/shutdown',
			scavenge: '/admin/scavenge'
		},
		users: {
			list: '/users/',
			get: '/users/%s',		// %s - user name
			create: '/users/',
			update: '/users/%s',	// %s - user name
			remove: '/users/%s',	// %s - user name
			disable: '/users/%s/command/disable',	// %s - user name
			enable: '/users/%s/command/enable',		// %s - user name	
			resetPassword: '/users/%s/command/reset-password'	// %s - user name
		},
		query: {
			create: '/projections/transient?',
			update: '%s/query?emit=no', // $s - query url
			state: '%s/state', // $s - query url
			commands: {
				enable: '%s/command/enable', // $s - query url
				disable: '%s/command/disable' // $s - query url
			}
		},
		projections: {
			any: '/projections/any',
			allNonTransient: '/projections/all-non-transient',
			disable: '/command/disable',
			enable: '/command/enable',
			create: '/projections/%s?', // %s - mode
			createStandard: '/projections/continuous?emit=yes&checkpoints=yes&enabled=yes&name=%s&type=%s',	// %s - name, %s - type,
			state: '%s/state', // %s - projection url
			result: '%s/result', // %s - projection url
			statistics: '%s/statistics',
			remove: '%s?', // %s - projection url
			query: '%s/query?config=yes',
			updateQuery: '%s/query?emit=',
			updatePlainQuery: '%s/query',
			queryWithoutConfig: '%s/query?config=no',
			readEvents: '/projections/read-events',
			commands: {
				reset: '%s/command/reset',
				enable: '%s/command/enable',
				disable: '%s/command/disable'
			}
		},
		streams: {
			base: '%s',
			recent: '/streams/$all/head/100?embed=rich',
			created: '/streams/$streams/head/50?embed=rich',
			events: '/streams/%s',  // %s - streamId
			eventDetails: '/streams/%s/%s?embed=tryharder',
			tryharder: '?embed=tryharder',
			metadata: '/streams/%s%s/head',
			updateAcl: '/streams/$$%s'
		}
	});

});