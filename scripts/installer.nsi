Unicode True
!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"
!ifndef VERSION
!define VERSION "0.5.6"
!endif
!ifndef PRODUCT_KEY
!define PRODUCT_KEY "QuietDesk"
!endif
Name "静隅 QuietDesk ${VERSION}"
OutFile "${OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\${PRODUCT_KEY}"
InstallDirRegKey HKCU "Software\${PRODUCT_KEY}" "InstallDir"
RequestExecutionLevel user
SetCompressor zlib
VIProductVersion "${VERSION}.0"
VIAddVersionKey /LANG=2052 "ProductName" "静隅 QuietDesk"
VIAddVersionKey /LANG=2052 "FileDescription" "静隅安装向导"
VIAddVersionKey /LANG=2052 "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=2052 "LegalCopyright" "QuietDesk contributors"
!define MUI_ICON "${RELEASE}\Assets\quietdesk.ico"
!define MUI_UNICON "${RELEASE}\Assets\quietdesk.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "给自己，一点安静。"
!define MUI_WELCOMEPAGE_TEXT "欢迎安装静隅。$\r$\n$\r$\n离线自然声音、在线电台、静默专注与双语阅读。$\r$\n$\r$\n按照向导即可完成安装。无需管理员权限，无需另装 .NET。$\r$\n升级保留声音组合、阅读记录与本机加密设置。"
!define MUI_FINISHPAGE_TITLE "静隅已准备好"
!define MUI_FINISHPAGE_TEXT "可通过桌面或开始菜单打开静隅。$\r$\n$\r$\n默认开启按键轻音效，可在设置中关闭。音乐不会自动播放。$\r$\n阅读 API 由你在本机配置；安装包不携带服务密钥。"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH
!insertmacro MUI_LANGUAGE "SimpChinese"

!macro CheckRunning
!ifndef QA
 System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\QuietDesk.Player.v1") p .r0'
 ${If} $0 != 0
  System::Call 'kernel32::CloseHandle(p r0)'
  MessageBox MB_OK|MB_ICONINFORMATION "请先从静隅托盘菜单选择“退出”，再运行安装或卸载。" /SD IDOK
  SetErrorLevel 2
  Abort
 ${EndIf}
!endif
!macroend
Function .onInit
 SetShellVarContext current
 SetRegView 64
 ${IfNot} ${RunningX64}
  MessageBox MB_OK|MB_ICONSTOP "静隅需要 64 位 Windows。" /SD IDOK
  Abort
 ${EndIf}
 !insertmacro CheckRunning
FunctionEnd

Section "静隅" SEC_MAIN
 SetShellVarContext current
 SetRegView 64
 SetOverwrite on
 !include "${INSTALL_MANIFEST}"
 SetOutPath "$INSTDIR"
 FileOpen $0 "$INSTDIR\.quietdesk-install" w
 FileWrite $0 "${PRODUCT_KEY}"
 FileClose $0
 WriteUninstaller "$INSTDIR\Uninstall.exe"
 CreateDirectory "$SMPROGRAMS\${PRODUCT_KEY}"
 CreateShortcut "$SMPROGRAMS\${PRODUCT_KEY}\静隅.lnk" "$INSTDIR\QuietDesk.exe" "" "$INSTDIR\Assets\quietdesk.ico"
 CreateShortcut "$DESKTOP\${PRODUCT_KEY}.lnk" "$INSTDIR\QuietDesk.exe" "" "$INSTDIR\Assets\quietdesk.ico"
 WriteRegStr HKCU "Software\${PRODUCT_KEY}" "InstallDir" "$INSTDIR"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}" "DisplayName" "静隅 QuietDesk"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}" "DisplayVersion" "${VERSION}"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}" "Publisher" "QuietDesk"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}" "DisplayIcon" "$INSTDIR\Assets\quietdesk.ico"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}" "InstallLocation" "$INSTDIR"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
 WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}" "NoModify" 1
 WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}" "NoRepair" 1
SectionEnd

Function un.onInit
 SetShellVarContext current
 SetRegView 64
 !insertmacro CheckRunning
 ClearErrors
 FileOpen $0 "$INSTDIR\.quietdesk-install" r
 IfErrors invalid
 FileRead $0 $1
 FileClose $0
 StrCmp $1 "${PRODUCT_KEY}" valid invalid
 invalid:
 MessageBox MB_OK|MB_ICONSTOP "安装目录标记不匹配，未删除文件。" /SD IDOK
 Abort
 valid:
FunctionEnd
Section "Uninstall"
 !include "${UNINSTALL_MANIFEST}"
 Delete "$INSTDIR\.quietdesk-install"
 Delete "$INSTDIR\Uninstall.exe"
 RMDir "$INSTDIR"
 Delete "$SMPROGRAMS\${PRODUCT_KEY}\静隅.lnk"
 RMDir "$SMPROGRAMS\${PRODUCT_KEY}"
 Delete "$DESKTOP\${PRODUCT_KEY}.lnk"
 DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}"
 DeleteRegKey HKCU "Software\${PRODUCT_KEY}"
 ; Never remove LOCALAPPDATA\QuietDesk, imported media, or unknown files.
SectionEnd
