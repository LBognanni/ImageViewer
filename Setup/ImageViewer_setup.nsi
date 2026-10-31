
; HM NIS Edit Wizard helper defines
!define PRODUCT_NAME "CodeMade Image Viewer"
!define PRODUCT_VERSION ${VERSION}
!define PRODUCT_EXE "imageviewer.exe"
!define PRODUCT_PUBLISHER "www.codemade.co.uk"
!define PRODUCT_WEB_SITE "http://www.codemade.co.uk/imageviewer/?from=winlink"
!define PRODUCT_DIR_REGKEY "Software\Microsoft\Windows\CurrentVersion\App Paths\CodeMadeImageViewer.exe"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
!define PRODUCT_UNINST_ROOT_KEY "HKLM"
!define PRODUCT_STARTMENU_REGVAL "NSIS:StartMenuDir"

; Offers the app in Windows' Default Apps settings, without taking over any file type
!macro RegisterExtension EXTENSION
  WriteRegStr HKLM "Software\ImageViewer\Capabilities\FileAssociations" "${EXTENSION}" "ImageViewer.Image"
!macroend

SetCompressor lzma

; MUI 1.67 compatible ------
!include "MUI.nsh"

; MUI Settings
!define MUI_ABORTWARNING
!define MUI_ICON "${NSISDIR}\Contrib\Graphics\Icons\arrow2-install.ico"
!define MUI_UNICON "${NSISDIR}\Contrib\Graphics\Icons\arrow2-uninstall.ico"

; Welcome page
!insertmacro MUI_PAGE_WELCOME
; License page
!insertmacro MUI_PAGE_LICENSE "eula.txt"
; Components page
!insertmacro MUI_PAGE_COMPONENTS
; Directory page
!insertmacro MUI_PAGE_DIRECTORY
; Start menu page
var ICONS_GROUP
!define MUI_STARTMENUPAGE_NODISABLE
!define MUI_STARTMENUPAGE_DEFAULTFOLDER "${PRODUCT_NAME}"
!define MUI_STARTMENUPAGE_REGISTRY_ROOT "${PRODUCT_UNINST_ROOT_KEY}"
!define MUI_STARTMENUPAGE_REGISTRY_KEY "${PRODUCT_UNINST_KEY}"
!define MUI_STARTMENUPAGE_REGISTRY_VALUENAME "${PRODUCT_STARTMENU_REGVAL}"
!insertmacro MUI_PAGE_STARTMENU Application $ICONS_GROUP
; Instfiles page
!insertmacro MUI_PAGE_INSTFILES
; Finish page
!define MUI_FINISHPAGE_RUN "$INSTDIR\${PRODUCT_EXE}"
!insertmacro MUI_PAGE_FINISH

; Uninstaller pages
!insertmacro MUI_UNPAGE_INSTFILES

; Language files
!insertmacro MUI_LANGUAGE "English"

; Reserve files
!insertmacro MUI_RESERVEFILE_INSTALLOPTIONS

; MUI end ------

Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "ImageViewer_Setup.exe"
InstallDir "$PROGRAMFILES64\${PRODUCT_NAME}"
InstallDirRegKey HKLM "${PRODUCT_DIR_REGKEY}" ""
ShowInstDetails show
ShowUnInstDetails show

Section "-MainSection" SEC01
  SetRegView 64
  SetOutPath "$INSTDIR"
  SetOverwrite ifnewer
  File "..\output\ImageViewer.exe"
  File "..\output\*.dll"
  File "eula.txt"

  CreateDirectory "$SMPROGRAMS\$ICONS_GROUP"
  CreateShortCut "$SMPROGRAMS\$ICONS_GROUP\${PRODUCT_NAME}.lnk" "$INSTDIR\ImageViewer.exe"
SectionEnd

Section "Add a Desktop Shortcut"
  CreateShortCut "$DESKTOP\${PRODUCT_NAME}.lnk" "$INSTDIR\ImageViewer.exe"
SectionEnd


;Section -AdditionalIcons
;  WriteIniStr "$INSTDIR\${PRODUCT_NAME}.url" "InternetShortcut" "URL" "${PRODUCT_WEB_SITE}"
;  CreateShortCut "$SMPROGRAMS\$ICONS_GROUP\Website.lnk" "$INSTDIR\${PRODUCT_NAME}.url"
;  CreateShortCut "$SMPROGRAMS\$ICONS_GROUP\Uninstall.lnk" "$INSTDIR\uninst.exe"
;SectionEnd

Section -Post
  WriteUninstaller "$INSTDIR\uninst.exe"
  WriteRegStr HKLM "${PRODUCT_DIR_REGKEY}" "" "$INSTDIR\${PRODUCT_EXE}"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayName" "$(^Name)"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "UninstallString" "$INSTDIR\uninst.exe"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayIcon" "$INSTDIR\${PRODUCT_EXE}"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "${PRODUCT_STARTMENU_REGVAL}" "$ICONS_GROUP"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "URLInfoAbout" "${PRODUCT_WEB_SITE}"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"

  WriteRegStr HKLM "Software\Classes\ImageViewer.Image" "" "Image"
  WriteRegStr HKLM "Software\Classes\ImageViewer.Image\DefaultIcon" "" "$INSTDIR\ImageViewer.exe,0"
  WriteRegStr HKLM "Software\Classes\ImageViewer.Image\shell\open\command" "" '"$INSTDIR\ImageViewer.exe" "%1"'
  WriteRegStr HKLM "Software\ImageViewer\Capabilities" "ApplicationName" "Image Viewer"
  WriteRegStr HKLM "Software\ImageViewer\Capabilities" "ApplicationDescription" "Fast and private image viewer"
  !insertmacro RegisterExtension ".bmp"
  !insertmacro RegisterExtension ".gif"
  !insertmacro RegisterExtension ".ico"
  !insertmacro RegisterExtension ".jfif"
  !insertmacro RegisterExtension ".jif"
  !insertmacro RegisterExtension ".jpeg"
  !insertmacro RegisterExtension ".jpg"
  !insertmacro RegisterExtension ".png"
  !insertmacro RegisterExtension ".wbmp"
  !insertmacro RegisterExtension ".webp"
  WriteRegStr HKLM "Software\RegisteredApplications" "Image Viewer" "Software\ImageViewer\Capabilities"
SectionEnd

Function un.onUninstSuccess
  HideWindow
  MessageBox MB_ICONINFORMATION|MB_OK "$(^Name) was successfully removed from your computer."
FunctionEnd

Function un.onInit
  MessageBox MB_ICONQUESTION|MB_YESNO|MB_DEFBUTTON2 "Are you sure you want to completely remove $(^Name) and all of its components?" IDYES +2
  Abort
FunctionEnd

Section Uninstall
  SetRegView 64
  ReadRegStr $ICONS_GROUP ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "${PRODUCT_STARTMENU_REGVAL}"
  Delete "$INSTDIR\ImageViewer.exe"
  Delete "$INSTDIR\*.dll"
  Delete "$INSTDIR\eula.txt"
  Delete "$INSTDIR\uninst.exe"

  ; Left behind by versions before the Avalonia rebuild
  Delete "$INSTDIR\ImageViewer.exe.config"
  Delete "$INSTDIR\icon.ico"
  Delete "$INSTDIR\freeimage-license.txt"
  Delete "$INSTDIR\vcredist_x64.exe"

  Delete "$DESKTOP\${PRODUCT_NAME}.lnk"
  Delete "$SMPROGRAMS\$ICONS_GROUP\${PRODUCT_NAME}.lnk"

  RMDir "$SMPROGRAMS\$ICONS_GROUP"
  RMDir "$INSTDIR"

  DeleteRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}"
  DeleteRegKey HKLM "${PRODUCT_DIR_REGKEY}"
  DeleteRegKey HKLM "Software\Classes\ImageViewer.Image"
  DeleteRegKey HKLM "Software\ImageViewer"
  DeleteRegValue HKLM "Software\RegisteredApplications" "Image Viewer"
  SetAutoClose true
SectionEnd
