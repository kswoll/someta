cd Nuget
mkdir build
cd build

copy ..\Someta.nuspec .
..\..\packages\NugetUtilities.1.0.17\UpdateVersion Someta.nuspec -SetVersion %APPVEYOR_BUILD_VERSION%
..\..\packages\NugetUtilities.1.0.17\UpdateReleaseNotes Someta.nuspec "%APPVEYOR_REPO_COMMIT_MESSAGE%"

mkdir lib
mkdir lib\netstandard2.0

copy ..\..\Someta\bin\Debug\netstandard2.0\Someta.* lib\netstandard2.0

..\..\Nuget\nuget pack Someta.nuspec

copy *.nupkg ..
rem nuget push *.nupkg ca9804b4-8f40-4b56-a35b-9ff23423a428 -source https://nuget.org

cd ..
rmdir build /S /Q
mkdir build
cd build

copy ..\Someta.Fody.nuspec .
..\..\packages\NugetUtilities.1.0.17\UpdateVersion Someta.Fody.nuspec -SetVersion %APPVEYOR_BUILD_VERSION%
..\..\packages\NugetUtilities.1.0.17\UpdateReleaseNotes Someta.Fody.nuspec "%APPVEYOR_REPO_COMMIT_MESSAGE%"

mkdir build
mkdir weaver
mkdir lib
mkdir lib\net45

copy ..\..\Someta.Fody\bin\Debug\netstandard2.0\Someta.Fody.* weaver
copy ..\Someta.Fody.props build
copy ..\Placeholder.txt lib\net45

..\..\Nuget\nuget pack Someta.Fody.nuspec

copy *.nupkg ..
rem nuget push *.nupkg ca9804b4-8f40-4b56-a35b-9ff23423a428 -source https://nuget.org

cd ..
rmdir build /S /Q
cd ..