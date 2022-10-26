# NLSPOLEventHandler
# Notes: To each Visual Studio compilation output. We chain a few lines of scripts
# together. In doing so, Make executes all of them in one process. We want the exit 
# status of this process to be the exit status of code compilation. So we add this 
# line of code at the end. Note that there is no line continuation character "\" at
# the end of this line because this is the end of a chain of commands to be executed
# in one shell process. However, the line before this should have a "\".
#		exit $$COMPILE_STATUS
#
# On the other hand, if we put MSVC call within a if statement where the whole compilation
# binary signing and copying to distribution directory are all in one line. That means
# the command "exit $$COMPILE_STATUS" cannot be the last commnand to be executed in a
# process. In this case, we need to check exit status before calling "exit $$COMPILE_STATUS".
#		if [ $$COMPILE_STATUS -ne 0 ]; then		\
#			exit $$COMPILE_STATUS ;				\
#		fi ;									\
# $(BUILDOUTPUTDIR) is $(NLBUILDROOT)/bin/$(BIN_DIR)
#
ifeq ($(NLBUILDROOT),)
  $(error NLBUILDROOT undefined.)
endif
# ifeq ($(NLEXTERNALGITDIR),)
 # $(error NLEXTERNALGITDIR undefined.)
# endif

# Variables

TARGETS_SP=prepare_assamble compile

# Release parameter

# any CPU the target we set it as x64
ifneq ($(TARGETENVARCH),x86)
	TARGETENVARCH=x64
endif

ifneq ($(BUILDTYPE), debug)
	BUILDTYPE=release
endif

# Makefile.vs contains target all which depends on $(TARGETS_SP)
include $(NLBUILDROOT)/build/Makefile.vs

$(info --------------------------------------------------------------------------)
$(info [Project])
$(info PROJECT=$(PROJECT))
$(info [Parameters])
$(info BUILDTYPE=$(BUILDTYPE))
$(info NLBUILDROOT=$(NLBUILDROOT))
# $(info NLEXTERNALGITDIR=$(NLEXTERNALGITDIR))
$(info BUILDOUTPUTDIR=$(BUILDOUTPUTDIR))
$(info ---------------------------------------------------------------------------)

NUGET_DIR=C:/Source/nuget.exe
DEPEND_DIR=$(NLBUILDROOT)/prod/NextLabs.EM.Teams/Assemblies/nxlib
Solution_FIl_1=$(NLBUILDROOT)/prod/NextLabs.EM.Teams/NextLabs.EM.Teams.sln
Solution_FIl_2=$(NLBUILDROOT)/prod/NextLabs.EM.Teams/UpdateSharePointApp.sln
Solution_FIl_3=$(NLBUILDROOT)/prod/NextLabs.EM.Teams/SharePointAddInForEMTeams.sln
MSBuild_Exe=$(PROGRAM_FILES_X86)/Microsoft Visual Studio/2019/Professional/MSBuild/Current/Bin/amd64/MSBuild.exe

.PHONY: prepare_assamble
prepare_assamble:
	@echo "Prepare assamble"
	@echo "Copy depend"
	@if [ ! -d $(DEPEND_DIR) ] ; then               \
		  mkdir -p $(DEPEND_DIR) ;                      \
	fi
	cp -pf C:\Jenkins\jobs\release_MSEnforcerCommon_master\workspace\bin\release_win_x64\QueryCloudAZSDK.dll  $(DEPEND_DIR)/
	cp -pf C:\Jenkins\jobs\release_MSEnforcerCommon_master\workspace\bin\release_win_x64\QueryCloudAZSDK.pdb  $(DEPEND_DIR)/

.PHONY: compile
compile:
	@echo ""
	@echo "Deploying $(PROJECT) ($(BUILDTYPE)) for NextLabs Enforcer for Microsoft Teams"
	@echo ""
	@echo "Command: $(MSVSIDE) publish $(Solution_FIl_1) --configuration Release"
	@echo ""
	$(MSVSIDE) publish $(Solution_FIl_1) --configuration Release ; \

	@echo ""
	@echo "Deploying $(PROJECT) ($(BUILDTYPE)) UpdateSharePointApp"
	@echo ""
	@echo "Command: $(MSBuild_Exe) $(Solution_FIl_2) /p:configuration="release";platform="x64";DeployOnBuild=true"
	@echo ""
	"$(MSBuild_Exe)" $(Solution_FIl_2) /p:configuration="release";platform="x64";DeployOnBuild=true ; \

	@echo ""
	@echo "Deploying $(PROJECT) ($(BUILDTYPE)) for SharePoint AddIn"
	@echo ""
	@echo "Command: $(MSBuild_Exe) /t:Package $(Solution_FIl_3) /p:configuration="release";DeployOnBuild=true"
	@echo ""
	"$(NUGET_DIR)" restore $(Solution_FIl_3) ; \
	"$(MSBuild_Exe)" /t:Package $(Solution_FIl_3) /p:configuration="release";platform="x64";DeployOnBuild=true ; \

	if [ ! -d $(BUILDOUTPUTDIR) ] ; then               \
		mkdir -p $(BUILDOUTPUTDIR) ;                      \
	fi
	cp -rf --preserve=timestamps ./UpdateSharePointApp/bin/Release/UpdateSharePointApp.exe $(BUILDOUTPUTDIR)
	cp -rf --preserve=timestamps ./SharePointAddInForEMTeams/bin/Release/app.publish/1.0.0.0/SharePointAddInForEMTeams.app $(BUILDOUTPUTDIR)
	cp -rf --preserve=timestamps ./SharePointAddInForEMTeams/bin/Release/app.publish/1.0.0.0/SharePointAddInForEMTeams.Web.SetParameters.xml $(BUILDOUTPUTDIR)
	cd $(NLBUILDROOT)/prod/NextLabs.EM.Teams/SharePointAddInForEMTeamsWeb/obj/Release/Package/PackageTmp/ ; zip -r $(BUILDOUTPUTDIR)/SharePointAddInForEMTeams.Web.zip *
	cd $(NLBUILDROOT)/prod/NextLabs.EM.Teams/NextLabs.EM.Teams/bin/Release/netcoreapp3.1/Publish/ ; zip -r $(BUILDOUTPUTDIR)/NextLabsEMTeams.Web.zip *

.PHONY: clean
clean:
	@if [ -e ./Makefile.inc ]; then \
		rm -rf $(HOSTAPPPROJECT)/obj $(PROVIDERPROJECT)/obj \
			obj* *.suo *.ncb *.pdb \
			$(BUILDOUTPUTDIR)/* ; \
	fi
