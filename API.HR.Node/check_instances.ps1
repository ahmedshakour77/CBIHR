$instances = @('localhost\SQLEXPRESS', 'localhost', '.', '(local)', '(localdb)\mssqllocaldb', "$env:COMPUTERNAME\SQLEXPRESS", "$env:COMPUTERNAME")
foreach ($inst in $instances) {
    try {
        $c = New-Object System.Data.SqlClient.SqlConnection("Server=$inst;Database=API_HR_Node;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=3")
        $c.Open()
        $cmd = $c.CreateCommand()
        $cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Employees' AND COLUMN_NAME = 'LastAllowanceDate'"
        $count = $cmd.ExecuteScalar()
        Write-Output "Instance [$inst] CONNECTED: LastAllowanceDate column count = $count"
        if ($count -eq 0) {
            Write-Output "Adding LastAllowanceDate to [$inst]..."
            $cmd.CommandText = "ALTER TABLE [Employees] ADD [LastAllowanceDate] date NULL;"
            $cmd.ExecuteNonQuery()
            Write-Output "Added to [$inst] successfully!"
        }
        $c.Close()
    }
    catch {
        Write-Output "Instance [$inst] FAILED: $($_.Exception.Message)"
    }
}
