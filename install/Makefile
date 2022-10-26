include $(NLBUILDROOT)/build/Makefile.ver

VERSION_STR=$(VERSION_MAJOR_SPE).$(VERSION_MINOR_SPE)
ASSEMBLY_DIR=assembly
PACKAGE_FILE=NextlabsEnforcerForMicrosoftTeams-$(VERSION_STR)-$(VERSION_BUILD_SHORT)-$(shell date +"%Y%m%d%H%M").zip
DEST_INSTALL_DIR=$(NLBUILDROOT)/install
REMOTE_DIR_RC=S:/build/release_candidate/EMTeams

.PHONY: all
all: installer

PHONY: installer
installer:
	@echo "INFO: Make installer"
	@if [ ! -d $(DEST_INSTALL_DIR) ]; then					\
		mkdir $(DEST_INSTALL_DIR) ;						\
	fi
	@echo "INFO: Zip assembly"
	cd $(NLBUILDROOT)/bin/release_win_x64/; zip -r $(DEST_INSTALL_DIR)/$(PACKAGE_FILE) *
	@echo "INFO: Copy Zip assembly to $(REMOTE_DIR_RC)"
	@cp -rf $(DEST_INSTALL_DIR)/$(PACKAGE_FILE) $(REMOTE_DIR_RC)/$(PACKAGE_FILE)
	
