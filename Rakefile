require 'bundler/setup'
require 'albacore'
require 'albacore/tasks/release'
require 'albacore/tasks/versionizer'
require 'albacore/ext/teamcity'

Configuration = ENV['CONFIGURATION'] || 'Release'

Albacore::Tasks::Versionizer.new :versioning

task :paket_bootstrap do
  system 'tools/paket.bootstrapper.exe', clr_command: true unless   File.exists? 'tools/paket.exe'
end

desc 'restore all nugets as per the packages.config files'
task :restore => :paket_bootstrap do
  system 'tools/paket.exe', 'restore', clr_command: true
end

desc 'merge clientlib assemblies'
task :merge do
  sh 'tools/ilmerge/ilmerge.exe', %W|/xmldocs /internalize /target:library
                                     /targetPlatform:"v4" /out:build/pkg
                                     EventStore.ClientAPI.dll
                                     #{FileList['src/EventStore.ClientAPI/bin/Release/*.dll'].
                                        exclude(/ClientAPI/)} |
end

directory 'build/pkg'

desc 'package nugets - finds all projects and package them'
nugets_pack :create_nugets => ['build/pkg', :versioning] do |p|
  p.configuration = Configuration
  p.files   = FileList['src/EventStore.ClientAPI/*.csproj']
  p.out     = 'build/pkg'
  p.exe     = 'packages/NuGet.CommandLine/tools/NuGet.exe'
  p.with_metadata do |m|
    m.id          = 'EventStore.Client.PreRelease'
    m.title       = 'EventStore.Client PreRelease'
    m.description = 'A client library for EventStore'
    m.authors     = 'Event Store LLP, all contributors to the repository'
    m.project_url = 'http://geteventstore.com'
    m.tags        = 'eventstore client tcp http'
    m.version     = ENV['NUGET_VERSION']
    m.icon_url    = 'http://geteventstore.com/assets/ouro-200.png'
    m.license_url = 'http://geteventstore.com/terms/licence/bsd/'
  end
  # p.gen_symbols
end

task :default => :create_nugets

task :ensure_nuget_key do
  raise 'missing env NUGET_KEY value' unless ENV['NUGET_KEY']
end

Albacore::Tasks::Release.new :release,
                             pkg_dir: 'build/pkg',
                             depend_on: [:create_nugets, :ensure_nuget_key],
                             nuget_exe: 'packages/NuGet.CommandLine/tools/NuGet.exe',
                             api_key: ENV['NUGET_KEY']
