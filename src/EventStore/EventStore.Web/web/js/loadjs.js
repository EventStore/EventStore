//creating by mmaksymiv v0.3
//script/css loader
//match script to class or id
//executed sam internall script for page


//Load external JavaScript files dynamically. If there are dependencies between multiple JS files, ensure main JS files loaded at the bottom of web page at least.
function loadScriptsAfterDocumentReady(_lazyLoadScripts, _loadto)
{
  if (_lazyLoadScripts && _lazyLoadScripts != null)
  {   
    var src = 0, type = 1, cssclass = 2;
	
    for (var i = 0; i < _lazyLoadScripts.length; i++)
    {
	    //set default tupe
	    if(_lazyLoadScripts[i][type] == undefined || _lazyLoadScripts[i][type] == "")
		  { 
			_lazyLoadScripts[i][type] = "script";
		  }
		  
  		//get last element in head  block
		if(typeof(_loadto) == "undefined" || _loadto == ""){
			_loadto = "head";
		}
  		var lastTag = $(_loadto).children()[$(_loadto).children().length-1];
  		
  		if(loadScriptsAfterDocumentReady.scriptload == undefined)
  		{
  			loadScriptsAfterDocumentReady.scriptload = [];
  		}
		  //check is script not load	
		  if(find(loadScriptsAfterDocumentReady.scriptload, _lazyLoadScripts[i][src]) == -1){
		  
  			loadScriptsAfterDocumentReady.scriptload.push( _lazyLoadScripts[i][src] );
  			
  			switch (_lazyLoadScripts[i][type]) {
  			  case "script":
  			    var matchproperty = "run";
  			    if(_lazyLoadScripts[i][cssclass] != undefined  && $(_lazyLoadScripts[i][cssclass]).length > 0){
    				  matchproperty = _lazyLoadScripts[i][cssclass];
    				}
    				if(_lazyLoadScripts[i][cssclass] != undefined  && $(_lazyLoadScripts[i][cssclass]).length == 0){
    				  matchproperty = "stop";
    				}

    				if(matchproperty != "stop"){
    					var scriptTag = document.createElement('script');
    					scriptTag.type = 'text/javascript';
    					scriptTag.src = _lazyLoadScripts[i][src];
    					lastTag.parentNode.insertBefore(scriptTag, lastTag);
    				}
    				//var firstScriptTag = document.getElementsByTagName('script')[0];
    				//firstScriptTag.parentNode.insertBefore(scriptTag, firstScriptTag);	
    				break;
  			  case "css":
    				var cssTag = document.createElement('link');
    				cssTag.rel = 'stylesheet';
    				cssTag.type = 'text/css';
    				cssTag.href = _lazyLoadScripts[i][src];
    				lastTag.parentNode.insertBefore(cssTag, lastTag);
    				break;
  			} //switch
		  }// if
			
    } // for
  } //if
}


//find if script allready load;
function find(array, value) {
  for(var i=0; i<array.length; i++) {
    if (array[i] == value) return i;
  }
  return -1;
}

 
//Execute the callback when document ready.
//Execute inline JavaScript code after document ready, at least at the bottom of web page.
function invokeLazyExecutedCallbacks(_lazyExecutedCallbacks)
{
    if (_lazyExecutedCallbacks && _lazyExecutedCallbacks.length > 0)
        for(var i=0; i<_lazyExecutedCallbacks.length; i++)
            _lazyExecutedCallbacks[i]();
}
 
//execute all deferring JS when document is ready by using jQuery.
jQuery(document).ready(function () {

    var scriptarr = new Array();
    scriptarr.push(['bootstrap/js/bootstrap.min.js', "script"]);
    scriptarr.push(['lib/jsrender/jsrender.js', "script"]);
    scriptarr.push(['js/es.menu.js', "script"]);

    /*for graph selection*/
    scriptarr.push(['js/charts/es.graphcontrol.js', "script", ".graphcontrol"]);

    loadScriptsAfterDocumentReady(scriptarr, ".scriptload");

    /*ExecutedCallbacks = new Array();
    ExecutedCallbacks.push(function ()
    {
    $("#extralinks a").click(function(){
    $(this).css({"color": "yellow", "font-size": "12px"});
    });
    });
    invokeLazyExecutedCallbacks(ExecutedCallbacks);*/

});
