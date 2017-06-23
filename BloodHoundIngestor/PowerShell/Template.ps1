﻿
function Invoke-BloodHound{
    <#
    .SYNOPSIS

        Runs the BloodHound C# Ingestor using reflection. The assembly is stored in this file.

    .DESCRIPTION

        Using reflection and assembly.load, load the compiled BloodHound C# ingestor into memory
        and run it without touching disk. Parameters are converted to the equivalent CLI arguments
        for the SharpHound executable and passed in via reflection. The appropriate function
        calls are made in order to ensure that assembly dependencies are loaded properly.

    .PARAMETER Verbose

        Enable verbose output mode. Will print a lot!

    .PARAMETER CollectionMethod

        Specifies the CollectionMethod being used. Possible value are:
            Group - Collect group membership information
            LocalGroup - Collect local admin information for computers
            Session - Collect session information for computers
            SessionLoop - Continuously collect session information until killed
            Trusts - Enumerate domain trust data
            ACL - Collect ACL (Access Control List) data
            ComputerOnly - Collects Local Admin and Session data
            GPOLocalGroup - Collects Local Admin information using GPO (Group Policy Objects)
            LoggedOn - Collects session information using privileged methods (needs admin!)
            Cache - Only builds the database cache
            Default - Collects Group Membership, Local Admin, Sessions, and Domain Trusts

    .PARAMETER Domain

        Specifies the domain to enumerate. If not specified, will enumerate the current
        domain your user context specifies.

    .PARAMETER SearchForest

        Expands data collection to include all domains in the forest. 

    .PARAMETER Stealth

        Use stealth collection options, will sacrifice data quality in favor of much reduced
        network impact

    .PARAMETER SkipGCDeconfliction

        Skip's Global Catalog deconfliction during session enumeration. This option
        can result in more inaccuracy in data.

    .PARAMETER Threads

        Specifies the number of threads to use during enumeration (Default 20)

    .PARAMETER PingTimeout

        Specifies timeout for ping requests to computers in milliseconds (Default 750)

    .PARAMETER SkipPing

        Skip all ping checks for computers. This option will most likely be slower as
        API calls will be made to all computers regardless of being up
        Use this option if ping is disabled on the network for some reason

    .PARAMETER LoopTime

        Amount of time to wait between session enumeration loops in minutes. This option
        should be used in conjunction with the SessionLoop enumeration method. 
        (Default 5 minutes)

    .PARAMETER MaxLoopTime

        Max amount of time to spend looping in minutes. Session looping will stop when this 
        time is exceeded. By default, looping will continue indefinitely

    .PARAMETER CSVFolder

        Folder to export CSVs too (Defaults to current directory)

    .PARAMETER CSVPrefix

        Prefix to add to your CSV Files (Default "")

    .PARAMETER URI

        The URI for the Neo4j REST API. Setting this option will turn off CSV output
        Format for this options is SERVER:PORT

    .PARAMETER UserPass

        Credentials for the Neo4j REST API. 
        Format for this option is username:password

    .PARAMETER DB

        Filename for the NoSQL Database used by bloodhound. (Default BloodHound.db)

    .PARAMETER InMemory

        Store database in memory instead of on disk. 
        This option can be very RAM intensive, use with caution!

    .PARAMETER RemoveDB

        Automatically delete the database on disk after running

    .PARAMETER ForceRebuild

        Force a rebuild of the BloodHound database

    .PARAMETER NoDB

        Enumerate without using the NoSQL DB.
        Only recommended for extremely large networks

    .PARAMETER Interval

        Interval to display progress during enumeration in milliseconds (Default 30000)
        
    .EXAMPLE

        PS C:\> Invoke-BloodHound

        Executes the default collection options and exports CSVs to the current directory

    .EXAMPLE
        
        PS C:\> Invoke-BloodHound -URI localhost:7474 -UserPass neo4j:BloodHound

        Executes default collection options and exports data to the Neo4j database using the
        REST API

    .EXAMPLE
        
        PS C:\> Invoke-BloodHound -CollectionMethod SessionLoop -LoopTime 1 -MaxLoopTime 10
    
        Executes session collection in a loop. Will wait 1 minute after each run to continue collection
        and will continue running for 10 minutes after which the script will exit
#>
    param(
        [String]
        [ValidateSet('Group', 'ComputerOnly', 'LocalGroup', 'GPOLocalGroup', 'Session', 'LoggedOn', 'Trusts', 'Cache','ACL', 'SessionLoop', 'Default')]
        $CollectionMethod = 'Default',

        [Switch]
        $SearchForest,

        [String]
        $Domain,

        [ValidateScript({ Test-Path -Path $_ })]
        [String]
        $CSVFolder = $(Get-Location),

        [ValidateNotNullOrEmpty()]
        [String]
        $CSVPrefix,

        [ValidateRange(1,50)]
        [Int]
        $Threads = 20,

        [Switch]
        $SkipGCDeconfliction,

        [Switch]
        $Stealth,

        [ValidateRange(50,1500)]
        [int]
        $PingTimeout = 750,

        [Switch]
        $SkipPing,

        [URI]
        $URI,

        [String]
        [ValidatePattern('.*:.*')]
        $UserPass,

        [String]
        [ValidateNotNullOrEmpty()]
        $DBFileName,

        [Switch]
        $InMemory,

        [Switch]
        $RemoveDB,

        [Switch]
        $NoDB,

        [Switch]
        $ForceRebuild,

        [ValidateRange(500,60000)]
        [int]
        $Interval,

        [Switch]
        $Verbose,

        [ValidateRange(1,50000000)]
        [int]
        $LoopTime,

        [ValidateRange(1,50000000)]
        [int]
        $MaxLoopTime
    )

    $vars = New-Object System.Collections.Generic.List[System.Object]

    $vars.Add("-c")
    $vars.Add($CollectionMethod);

    if ($Domain){
        $vars.Add("-d");
        $vars.Add($Domain);
    }

    if ($SearchForest){
        $vars.Add("-s");
    }

    if ($CSVFolder){
        $vars.Add("-f")
        $vars.Add($CSVFolder)
    }

    if ($CSVPrefix){
        $vars.Add("-p")
        $vars.Add($CSVPrefix)
    }

    if ($Threads){
        $vars.Add("-t")
        $vars.Add($Threads)
    }

    if ($SkipGCDeconfliction){
        $vars.Add("--SkipGCDeconfliction")
    }

    if ($Stealth){
        $vars.Add("--Stealth")
    }

    if ($PingTimeout){
        $vars.Add("--PingTimeout")
        $vars.Add($PingTimeout)
    }

    if ($SkipPing){
        $vars.Add("--SkipPing");
    }

    if ($URI){
        $vars.Add("--URI")
        $vars.Add($URI)
    }

    if ($UserPass){
        $vars.Add("--UserPass")
        $vars.Add($UserPass)
    }

    if ($DBFileName){
        $vars.Add("--DBFileName")
        $vars.Add($DBFileName)
    }

    if ($InMemory){
        $vars.Add("--InMemory")
    }

    if ($RemoveDB){
        $vars.Add("--RemoveDB")
    }

    if ($ForceRebuild){
        $vars.Add("--ForceRebuild")
    }

    if ($Verbose){
        $vars.Add("-v")
    }

    if ($Interval){
        $vars.Add("-i");
        $vars.Add($Interval)
    }

    if ($NoDB){
        $vars.Add("--NoDB");
    }

    if ($LoopTime){
        $vars.Add("--LoopTime");
        $vars.Add($LoopTime);
    }

    if ($MaxLoopTime){
        $vars.Add("--MaxLoopTime");
        $vars.Add($MaxLoopTime);
    }

    $passed = [string[]]$vars.ToArray()

    #ENCODEDCONTENTHERE
}
