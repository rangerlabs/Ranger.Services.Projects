﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Ranger.Services.Projects.Data;

namespace Ranger.Services.Projects.Data.Migrations
{
    [DbContext(typeof(ProjectsDbContext))]
    partial class ProjectsDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn)
                .HasAnnotation("ProductVersion", "2.2.4-servicing-10062")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            modelBuilder.Entity("Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnName("id");

                    b.Property<string>("FriendlyName")
                        .HasColumnName("friendly_name");

                    b.Property<string>("Xml")
                        .HasColumnName("xml");

                    b.HasKey("Id")
                        .HasName("pk_data_protection_keys");

                    b.ToTable("data_protection_keys");
                });

            modelBuilder.Entity("Ranger.Services.Projects.Data.ProjectStream<Ranger.Services.Projects.Data.Project>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnName("id");

                    b.Property<string>("Data")
                        .IsRequired()
                        .HasColumnName("data")
                        .HasColumnType("jsonb");

                    b.Property<string>("DatabaseUsername")
                        .IsRequired()
                        .HasColumnName("database_username");

                    b.Property<string>("Event")
                        .IsRequired()
                        .HasColumnName("event");

                    b.Property<DateTime>("InsertedAt")
                        .HasColumnName("inserted_at");

                    b.Property<string>("InsertedBy")
                        .IsRequired()
                        .HasColumnName("inserted_by");

                    b.Property<Guid>("ProjectUniqueConstraintProjectId")
                        .HasColumnName("project_unique_constraint_project_id");

                    b.Property<Guid>("StreamId")
                        .HasColumnName("stream_id");

                    b.Property<int>("Version")
                        .HasColumnName("version");

                    b.HasKey("Id")
                        .HasName("pk_project_streams");

                    b.HasIndex("ProjectUniqueConstraintProjectId")
                        .HasName("ix_project_streams_project_unique_constraint_project_id");

                    b.ToTable("project_streams");
                });

            modelBuilder.Entity("Ranger.Services.Projects.Data.ProjectUniqueConstraint", b =>
                {
                    b.Property<Guid>("ProjectId")
                        .HasColumnName("project_id");

                    b.Property<Guid>("ApiKey")
                        .HasColumnName("api_key");

                    b.Property<string>("DatabaseUsername")
                        .IsRequired()
                        .HasColumnName("database_username");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnName("name")
                        .HasMaxLength(140);

                    b.HasKey("ProjectId")
                        .HasName("pk_project_unique_constraints");

                    b.HasIndex("DatabaseUsername", "ApiKey")
                        .IsUnique();

                    b.HasIndex("DatabaseUsername", "Name")
                        .IsUnique();

                    b.ToTable("project_unique_constraints");
                });

            modelBuilder.Entity("Ranger.Services.Projects.Data.ProjectStream<Ranger.Services.Projects.Data.Project>", b =>
                {
                    b.HasOne("Ranger.Services.Projects.Data.ProjectUniqueConstraint", "ProjectUniqueConstraint")
                        .WithMany()
                        .HasForeignKey("ProjectUniqueConstraintProjectId")
                        .HasConstraintName("fk_project_streams_project_unique_constraints_project_unique_con~")
                        .OnDelete(DeleteBehavior.Cascade);
                });
#pragma warning restore 612, 618
        }
    }
}
