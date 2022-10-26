#!/usr/bin/perl
#
# DESCRIPTION
#	This script finds a Policy Adaptor SDK zip file of the latest build under the
#	specified PolicyAdaptorSDK folder (e.g., PolicyAdapterSDK-6.0.0.524-6PS_main-20120630.zip). 
#	The full path of the PolicyAdaptorSDK zip file is saved in a Bash script. 
#
#	This script is used form in $NLBUILDROOT/configure. This script is only used 
#	for finding a latest build under s:\build\release_candidate and
#	s:\build\pcv. If you need a particular version of releases Policy Adaptor SDK, 
#	there is not need to search for it because there is always only one version 
#	available.
#
#	The scanning logic makes the following assumptions:
#
#	1. The start folder specified is at the version # level. 
#		For example:
#		- S:/build/pcv/PolicyAdapterSDK/6.0.0.524
#		- S:/build/release_candidate/PolicyAdapterSDK/6.0.0.0
#	2. 	The build number folder may be numeric or start with a build #. 
#		For example:
#		- 128
#		- 26PS_perf
#		- 81PS_officepep
#	3. Policy Adaptor SDK zip file always starts with PolicyAdapterSDK.
#		For example:
#		- PolicyAdapterSDK-6.0.0.0-11-20120630.zip
#		- PolicyAdapterSDK-6.0.0.524-6PS_main-20120630.zip
#
#	The following are examples on how to set $XLIB_POLICY_ADAPTOR_SDK_ZIP_FILE
#	in $NLBUILDROOT/configure.
#
#	1. SDK in s:\build\release_artifacts
#		Dependencies used by Makefile.xlib
#		if [ "$XLIB_POLICY_ADAPTOR_SDK_ZIP_FILE" == "" ]; then
#			XLIB_POLICY_ADAPTOR_SDK_ZIP_FILE="S:/build/release_artifacts/PolicyAdapterSDK/6.0.0.0/11/PolicyAdapterSDK-6.0.0.0-11-20120630.zip"
#		fi
#
#	2. Find latest SDK in s:\build\pcv. Note that this script is under $NLBUILDROOT/build
#	   for C/C++ source tree. For Java source tree, it is under $NLBUILDROOT/scripts.
#		Dependencies used by Makefile.xlib
#		if [ "$XLIB_POLICY_ADAPTOR_SDK_ZIP_FILE" == "" ]; then
#			perl $NLBUILDROOT/scripts/getNewestPolicyAdaptorSDK.pl \
#				--startpath=S:/build/pcv/PolicyAdapterSDK/6.0.0.524 \
#				--outfile=build.config.sdk --varname=XLIB_POLICY_ADAPTOR_SDK_ZIP_FILE
#			source build.config.sdk
#		fi


use strict;
use warnings;

use Getopt::Long;

print "NextLabs Newest Policy Adaptor SDK Locator v0.1\n";


#
# Global variables
#

my	$g_help = 0;
my	$g_verbose = 0;
my	$g_startPath = '';
my	$g_outFile = '';
my	$g_varName = '';


#
# Functions
#

# -----------------------------------------------------------------------------
# Print usage

sub printUsage
{
	print "usage: getNewestPolicyAdaptorSDK.pl [--verbose] --startpath=<path> --outfile=<file>\n";
	print "         --varname=<name>\n";
	print "       getNewestPolicyAdaptorSDK.pl <-h|--help>\n";
	print "  help          Print this message (also -h)\n";
	print "  outfile       Output script file containing policy adaptor SDK zip file.\n";
	print "  startpath     Absolute path to a folder in build repository. The folder should contain a list of\n";
	print "                folders identified by build #s. For example:\n";
	print "                    S:/build/pcv/PolicyAdapterSDK/6.0.0.524\n";
	print "                    S:/build/release_candidate/PolicyAdapterSDK/6.0.0.0\n";
	print "  varname       Variable name to be used in output file.\n";
	print "  verbose       Print detailed messages\n";
}

# -----------------------------------------------------------------------------
# Parse command line arguments

sub parseCommandLine()
{
	#
	# Parse arguments
	#
	
	# GetOptions() key specification:
	#	option			Given as --option or not at all (value set to 0 or 1)
	#	option!			May be given as --option or --nooption (value set to 0 or 1)
	#	option=s		Mandatory string parameter: --option=somestring
	#	option:s		Optional string parameter: --option or --option=somestring	
	#	option=i		Mandatory integer parameter: --option=35
	#	option:i		Optional integer parameter: --option or --option=35	
	#	option=f		Mandatory floating point parameter: --option=3.14
	#	option:f		Optional floating point parameter: --option or --option=3.14	
		
	if (!GetOptions(
			'help|h' => \$g_help,				# --help or -h
			'startPath=s' => \$g_startPath,		# --startPath
			'outFile=s' => \$g_outFile,			# --outFile
			'varName=s' => \$g_varName,	        # --varName
			'verbose|v' => \$g_verbose			# --verbose
		))
	{
		return 0;
	}

	#
	# Help
	#
	
	if ($g_help == 1)
	{
		return 1;
	}

	# Check for errors
	if ($g_startPath eq '')
	{
		print STDERR "Missing start path\n";
		return 0;
	}
	
	if (! -d $g_startPath)
	{
		print STDERR "Start path does not exists - $g_startPath\n";
		return 0;
	}

	if ($g_outFile eq '')
	{
		print STDERR "Missing output file\n";
		return 0;
	}

	if ($g_varName eq '')
	{
		print STDERR "Missing variable name\n";
		return 0;
	}
	
	return 1;
}

# -----------------------------------------------------------------------------
# Find newest product build folder (largest build number)
#
# Description
#	The path passed in should look like this:
#	S:/build/pcv/PolicyAdapterSDK/6.0.0.524
#
# Return Value:
#	Return a value > 0 if a folder matches [0-9]+ or 
#	[0-9]+(PS|PC)_<branch name>. Return 0 if there is no match.
#	Build folder name is returned in the $refBuf parameter.

sub getNewestPolicyAdaptorSDKBuildFolder()
{
	my	($path, $refBuf) = @_;

	# Debug
	if ($g_verbose)
	{	
		print "Start path = $path\n";
	}
	
	# Get largest build number
	my	$maxVal = 0;
	my	$folderName = '';
	
	opendir(HANDLE, "$path") || die "ERROR: Failed to open directory $path";
	
	while (my $entry = readdir(HANDLE))
	{
		# Debug
		if ($g_verbose)
		{	
			print "  entry = $entry\n";
		}
		
		# Check file name
		if ($entry =~ /^(\d+)/)
		{
#			print "Build number = $1\n";
	
			if ($1 > $maxVal)
			{
				$maxVal = $1;
				$folderName = $entry;
			}		
		}
	}
	
	closedir(HANDLE);
	
	# Debug
	if ($g_verbose)
	{	
		print "Max value   = $maxVal\n";
		print "Folder name = $folderName\n";
	}
	
	# Return folder name
	if ($maxVal == 0)
	{
		return 0;
	}
	
	${$refBuf} = $folderName; 
	return 1;	
}

# -----------------------------------------------------------------------------
# Find policy adaptor SDK zip file (PolicyAdapterSDK-<version>-<build #>-<date>.zip)
#
# Description
#	The path passed in should look like this:
#	S:/build/pcv/PolicyAdapterSDK/6.0.0.524/101PS_test
#
# Return Value:
#	Return a value > 0 if a file name matches *-bin.zip. Return 0 if there is no match.
#	Policy adaptor zip file name is returned in the $refBuf parameter.

sub getPolicyAdaptorSDKZipFile()
{
	my	($path, $refBuf) = @_;

	# Debug
	if ($g_verbose)
	{	
		print "Start path = $path\n";
	}
	
	# Get policy adaptor zip file
	my	$found = 0;
	my	$fileName = '';
	
	opendir(HANDLE, "$path") || die "ERROR: Failed to open directory $path";
	
	while (my $entry = readdir(HANDLE))
	{
		# Debug
		if ($g_verbose)
		{	
			print "  entry = $entry\n";
		}

		# Check file name
		if ($entry =~ /^(PolicyAdapterSDK-.*\.zip)$/)
		{
#			print "Matched = $1\n";
			
			$found = 1;
			$fileName = $1;
			last;
		}
	}
	
	closedir(HANDLE);
	
	# Debug
	if ($g_verbose)
	{	
		print "Found     = $found\n";
		print "File name = $fileName\n";
	}
		
	# Return file name
	if (!$found)
	{
		return 0;
	}
	
	${$refBuf} = $fileName; 
	return 1;	
}


#
# Main Program
#

# Parse command line arguements
my	$argCount = scalar(@ARGV);

if ($argCount < 1 || $ARGV[0] eq "-h" || $ARGV[0] eq "--help")
{
	printUsage;
	exit 0;
}

if (!&parseCommandLine())
{
	exit 1;
}
	
if ($g_help == 1)
{
	printUsage;
	exit 0;
}

# Print parameters
print "Parameters:\n";
print "  g_startPath       = $g_startPath\n";
print "  g_outFile         = $g_outFile\n";
print "  g_varName         = $g_varName\n";

# Process command
my	$buildFolder = '';

if (!&getNewestPolicyAdaptorSDKBuildFolder($g_startPath, \$buildFolder))
{
	exit 1;
}

my	$path = $g_startPath . '/' . $buildFolder;
my	$fileName = '';

if (!&getPolicyAdaptorSDKZipFile($path, \$fileName))
{
	exit 1;
}

my	$file = $path . '/' . $fileName;
	
print "Found $file\n";


#
# Write output file
#

open FILE, ">$g_outFile" || die "Error opening output file $g_outFile\n";
binmode FILE;

print FILE <<"EOT";
#!/bin/bash

export $g_varName="$file"
EOT

close FILE;

exit 0;
