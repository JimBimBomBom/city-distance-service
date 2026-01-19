using FluentMigrator;

[Migration(1)]
public class Create : Migration
{
    public override void Up()
    {
        if (!Schema.Table("cities").Exists())
        {
            Create
                .Table("cities")
                .WithColumn("CityId")
                .AsGuid()
                .PrimaryKey()
                .WithColumn("CityName")
                .AsString(100)
                .NotNullable()
                .WithColumn("Latitude")
                .AsDecimal(10, 8)
                .NotNullable()
                .WithColumn("Longitude")
                .AsDecimal(10, 8)
                .NotNullable();
        }
    }

    public override void Down()
    {
        Delete.Table("cities");
    }
}

[Migration(2)]
public class AddDefaultId : Migration
{
    public override void Up()
    {
        Alter
            .Table("cities")
            .AlterColumn("CityId")
            .AsGuid()
            .NotNullable()
            .WithDefaultValue(SystemMethods.NewGuid);
    }

    public override void Down()
    {
        Delete.Column("DefaultId").FromTable("cities");
    }
}

[Migration(3)]
public class AddCityNameIndex : Migration
{
    public override void Up()
    {
        Create.Index("IX_Cities_CityName").OnTable("cities").OnColumn("CityName").Unique();
    }

    public override void Down()
    {
        Delete.Index("IX_Cities_CityName").OnTable("cities");
    }
}

[Migration(4)]
public class UpdateCitiesSchema : Migration
{
    public override void Up()
    {
        Delete.PrimaryKey("PK_cities").FromTable("cities");

        Alter.Column("CityId").OnTable("cities")
            .AsString(20)
            .NotNullable();

        Create.PrimaryKey("PK_cities")
            .OnTable("cities")
            .Column("CityId");

        Alter.Column("CityName")
            .OnTable("cities")
            .AsString(255)
            .NotNullable();
    }

    public override void Down()
    {
        // Revert column types to previous state
        Delete.PrimaryKey("PK_cities").FromTable("cities");

        Alter.Column("CityId").OnTable("cities")
            .AsInt32().Identity().NotNullable();

        Alter.Column("CityName").OnTable("cities")
            .AsString(100).NotNullable();

        Create.PrimaryKey("PK_cities")
            .OnTable("cities")
            .Column("CityId");
    }
}

[Migration(5)]
public class CreateSyncStateTable : Migration
{
    public override void Up()
    {
        Create.Table("sync_state")
            .WithColumn("SyncKey").AsString(50).PrimaryKey()
            .WithColumn("LastSync").AsDateTime().NotNullable();

        // Insert initial row
        Insert.IntoTable("sync_state")
            .Row(new
            {
                SyncKey = "CitySync",
                LastSync = new DateTime(2000, 1, 1)
            });
    }

    public override void Down()
    {
        Delete.Table("sync_state");
    }
}

[Migration(6)]
public class RemoveUniqueCityNameIndex : Migration
{
    public override void Up()
    {
        // Drop the unique index
        Delete.Index("IX_Cities_CityName").OnTable("cities");

        // Re-create it as a standard non-unique index for performance
        Create.Index("IX_Cities_CityName").OnTable("cities").OnColumn("CityName");
    }

    public override void Down()
    {
        // Revert to unique if we roll back
        Delete.Index("IX_Cities_CityName").OnTable("cities");
        Create.Index("IX_Cities_CityName").OnTable("cities").OnColumn("CityName").Unique();
    }
}
