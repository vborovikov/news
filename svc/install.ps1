$params = @{
  Name = 'Newsmaker'
  BinaryPathName = 'C:\Tools\News\Newsmaker.exe'
  Credential = New-Object System.Management.Automation.PSCredential ('NT AUTHORITY\LocalService', (New-Object System.Security.SecureString))
  DependsOn = @('MSMQ', 'MSSQLSERVER')
  DisplayName = 'News Aggregator'
  StartupType = 'Automatic'
  Description = 'News aggregator service'
}
New-Service @params