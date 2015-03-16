@echo off
copy ..\x64\release\obs.exe .\
copy ..\x64\release\obsapi.dll .\
copy ..\x64\release\dshowplugin.dll .\plugins
copy ..\x64\release\graphicscapture.dll .\plugins
copy ..\release\graphicscapturehook.dll .\plugins\graphicscapture
copy ..\x64\release\graphicscapturehook64.dll .\plugins\graphicscapture
copy ..\x64\release\noisegate.dll .\plugins
copy ..\x64\release\psvplugin.dll .\plugins
copy ..\x264\libs\64bit\libx264-142.dll .\
copy ..\Release\injectHelper.exe .\plugins\graphicscapture
copy ..\x64\Release\injectHelper64.exe .\plugins\graphicscapture
copy ..\x64\Release\ObsNvenc.dll .\
