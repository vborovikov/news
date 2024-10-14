# News at Home
Feed reader app

## How to build and run

To run the project follow these steps:

1. Clone the repository
2. Create the app database
3. Modify appsettings.json or secrets.json to point to the database
4. Build the project in Visual Studio/VS Code/dotnet CLI
5. Run the web app and the service program

Assuming you have the SQL Server LocalDB installed with the default instance named `MSSQLLocalDB`, you can create the database named `News` like this:

```
PS> cd .\news\
PS> sqlcmd -S '(LocalDB)\MSSQLLocalDB' -i .\db\db.sql -I
```

SQL Server LocalDB can suffice for this project but Express or Developer edition is recommended, so that you can use fulltext search capabilities. To enable fulltext search, run the following command (`.` here means the default SQL Server instance):

```
PS> sqlcmd -S '.' -i .\db\db_fulltext.sql -I
```

Add the following lines to `appsettings.json` or `secrets.json` in News.App and News.Service:
```
{
    "ConnectionStrings": {
        "DefaultConnection": "Server=(LocalDB)\\MSSQLLocalDB;Database=News;Trusted_Connection=True;MultipleActiveResultSets=true"
    }
}
```

The app expects the folder `C:\Tools\News` to exist. This folder is used to store the imported OPML files. You can change the path in the `appsettings.json` or `secrets.json`.

For News.App add the following lines:
```
{
    "Newsreader": {
        "OpmlPath": "C:\\Another\\Path\\"
    }
}
```

For News.Service add the following lines:
```
{
    "Newsmaker": {
        "OpmlPath": "C:\\Another\\Path\\"
    }
}
```