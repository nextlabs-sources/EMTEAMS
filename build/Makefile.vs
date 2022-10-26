# =============================================================================
# Top-level makefile include by makefiles that wrap around VisualStudio projects
# =============================================================================

ifeq ($(ProgramW6432), )
	ENV_OS=x86
	PROGRAM_FILES_X86=C:/Program Files
else
	ENV_OS=x64
	PROGRAM_FILES_X86=C:/Program Files (x86)
endif

OFFICIALCERT=0
SIGNTOOL_OFFICIAL_TOOL=$(PROGRAM_FILES_X86)/Windows Kits/8.0/bin/x64/signtool.exe
SIGNTOOL_OFFICIAL_ARGS=sign /ac c:/release/bin/DigiCertAssuredIDRootCA.cer /f c:/release/bin/NextLabs.pfx /p IiVf1itvOrqJ /n "NextLabs Inc." /fd sha256 /tr http://timestamp.digicert.com
SIGNTOOL_OFFICIAL='$(SIGNTOOL_OFFICIAL_TOOL)' $(SIGNTOOL_OFFICIAL_ARGS)

# MSVSIDE=O:/common7/IDE/devenv.com
MSVSIDE=Q:/dotnet.exe

ifeq ($(TARGETENVARCH),)
	TARGETENVARCH=x86
endif

ifneq ($(BUILDTYPE), release)
	BUILDTYPE=debug
endif

ifeq ($(BIN_DIR),)
	BIN_DIR=$(BUILDTYPE)_win_$(TARGETENVARCH)
endif

BUILDOUTPUTDIR=$(NLBUILDROOT)/bin/$(BIN_DIR)

ifeq ($(VERSION_BUILD), )
	VERSION_BUILD=$(shell date +"%y.%j.%H%M")DX-$(HOSTNAME)-$(USERNAME)-$(shell date +"%Y.%m.%d-%H:%M")
endif


$(info --------------------------------------------------------------------------)
$(info [Targets])
$(info PROJECT=$(PROJECT))
$(info OFFICIALCERT=$(OFFICIALCERT))
$(info RCSRC=$(RCSRC))
$(info [Parameters])
$(info BUILDTYPE=$(BUILDTYPE))
$(info TARGETENVARCH=$(TARGETENVARCH))
$(info NLBUILDROOT=$(NLBUILDROOT))
$(info NLEXTERNALDIR=$(NLEXTERNALDIR))
$(info NLEXTERNALDIR2=$(NLEXTERNALDIR2))
$(info BUILDOUTPUTDIR=$(BUILDOUTPUTDIR))
$(info BIN_DIR=$(BIN_DIR))
$(info [VERSION])
$(info PRODUCT=$(VERSION_PRODUCT))
$(info RELEASE=$(VERSION_MAJOR).$(VERSION_MINOR).$(VERSION_MAINTENANCE).$(VERSION_PATCH) ($(VERSION_BUILD)))
$(info ---------------------------------------------------------------------------)


.PHONY: all
all: versionInfo $(TARGETS_SP)

.PHONY: versionInfo
versionInfo:
	@if [ "$(RCSRC)" != "" ]; then \
		perl $(NLBUILDROOT)/build/updateVersionInfo_make.pl $(RCSRC) $(VERSION_MAJOR) $(VERSION_MINOR) $(VERSION_MAINTENANCE) $(VERSION_PATCH) "$(VERSION_BUILD)" "$(VERSION_PRODUCT)" $(TARGETENVARCH); \
		echo " --- Modified .rc file ---" ; \
		egrep "FILEVERSION|PRODUCTVERSION|CompanyName|FileDescription|FileVersion|LegalCopyright|ProductName|ProductVersion" $(RCSRC) ; \
	fi
