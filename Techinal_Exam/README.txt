Instructions for Database Configuration 

The Application use Entity Framework Core as a Database Connection it does not need to create the database manually instead a command line on the Package Manager Console will generate the Database and Table (This is method is called Migration)

- The Model Class Properties will act as a Table Columns on the Database 

Step 1: Open appsettings.json and find this code:

"AllowedHosts": "*",
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=DocumentDB;Trusted_Connection=True;TrustServerCertificate=True;"
},

In order to successfully connect the API to the database a proper SQL Server should be established in this connection string

Step 2: In order to successfully Migrate you need to type these commands on the Package Manager Console

- Add-Migration "YourMigrationName" - (you named your own migration name)
- Update Database

Check the SQL server (recommended MS SQL) if the Database and Table is successfully created.

Instructions for Running the Application

The .Net Core Web API runs on Visual Studio 2022 (now 2026) Visual Studio should be installed in the system with the latest .Net Core Version.

Note: I configure the API once it runs another browser will pop up this web page is called Scalar a Testing tool for API endpoints (POST, DELETE, etc.)

Instruction for Automated Test 

Navigate on the Menu Tab and Click Test Explorer 

List of Implemented Endpoints

[POST] -for uploading file and recording it through the database
[GET] -retrieving the file ID (GUID) 
[GET] -/Download retrieves and download the file 

Software Architecture

The architecture that Ive used is MVC (Model View Control), this is my navigation to my web development career since its has a linear pattern on both debugging and development

In Database I use MS SQL for storage since i use Entity Framework and Entity Framework is portable.


Limitations of the Project

It only uploads file... thats it 

AI Assisted Development. 

Claude AI help me for code generation debugging and reccomendation for clean architecture and efficient codebase. 






