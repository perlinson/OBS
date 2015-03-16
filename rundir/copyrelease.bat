@echo off
copy ..\release\obs.exe .\
copy ..\release\obsapi.dll .\
copy ..\release\dshowplugin.dll .\plugins
copy ..\release\graphicscapture.dll .\plugins
copy ..\release\graphicscapturehook.dll .\plugins\graphicscapture
copy ..\x64\release\graphicscapturehook64.dll .\plugins\graphicscapture
copy ..\release\noisegate.dll .\plugins
copy ..\release\psvplugin.dll .\plugins
copy ..\x264\libs\32bit\libx264-142.dll .\
copy ..\Release\injectHelper.exe .\plugins\graphicscapture
copy ..\x64\Release\injectHelper64.exe .\plugins\graphicscapture
copy ..\Release\ObsNvenc.dll .\
