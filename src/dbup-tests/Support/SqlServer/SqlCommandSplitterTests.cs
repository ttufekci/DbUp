﻿using System;
using NUnit.Framework;
using System.Text;
using System.Linq;
using DbUp.Support;
using Shouldly;
using Xunit;

namespace DbUp.Tests.Support.SqlServer
{

    public class SqlCommandSplitterTests
    {
        private readonly SqlCommandSplitter sut;

        public SqlCommandSplitterTests()
        {
            sut = new SqlCommandSplitter();
        }

        [Fact]
        public void does_not_split_go_in_column_name()
        {
            var statement = @"CREATE PROCEDURE dbo.GetDetails

    @AccountId uniqueidentifier

AS
BEGIN

SELECT AccountId,
        EstimatedInCents,
        OccupationInCents,
        GovernmentInCents".Replace("\r\n", "\n");


            var commands = sut.SplitScriptIntoCommands(statement).ToArray();

            commands.Count().ShouldBe(1);
            commands[0].ShouldBe(statement);
        }

        [Theory]
        [InlineData("GO", 0)]
        [InlineData(" GO", 0)]
        [InlineData("GO ", 0)]
        [InlineData("GO\n", 0)]
        [InlineData("GO\nGO--Dummy comment", 1)]
        [InlineData("\nGO", 0)]
        [InlineData("--Dummy comment\nGO", 1)]
        [InlineData("GO--Dummy comment", 1)]
        public void should_correctly_recognize_go_statements(string SqlText, int expectedNumberOfCommands)
        {
            var commands = sut.SplitScriptIntoCommands(SqlText).ToArray();

            commands.ShouldNotBeNull();
            commands.Count().ShouldBe(expectedNumberOfCommands);
        }

        [Fact]
        public void should_treat_bracketed_text_as_single_item()
        {
            var sb = new StringBuilder();
            sb.AppendLine("SELECT 1 as [']");
            sb.AppendLine("GO");
            sb.AppendLine("Select 2");
            var commands = sut.SplitScriptIntoCommands(sb.ToString());
            commands.Count().ShouldBe(2);
        }

        [Fact]
        public void multiple_brackets_should_escape_properly()
        {
            var sql = "Select 1 as [[a]][b]][c]]]";
            var commands = sut.SplitScriptIntoCommands(sql);
            commands.Count().ShouldBe(1);
        }

        [Fact]
        public void should_split_statements_on_go_and_handle_comments()
        {

            var sqlGo = "GO";
            var sqlGoWithTerminator = "GO;";
            var sqlBuilder = new StringBuilder();

            // Sql command with a multiline comment containing a GO.
            sqlBuilder.AppendLine(@"/*"); // Start of sql comment block.
            sqlBuilder.AppendLine(@"multi line comment 1.");
            sqlBuilder.AppendLine(@"GO");
            sqlBuilder.AppendLine(@"--");
            sqlBuilder.AppendLine(@"Other comment text");
            sqlBuilder.AppendLine(@"Go");
            sqlBuilder.AppendLine(@"*/");  // End of sql comment block.
            sqlBuilder.AppendLine(@"INSERT INTO A (GO) VALUES (1);");

            var sqlCommandWithMultiLineComment = sqlBuilder.ToString();
            sqlBuilder.Clear();

            // Sql command with a single line comment (no end dashes) containing a GO.
            sqlBuilder.AppendLine("--Single line Comment no end comment dashes GO");
            sqlBuilder.AppendLine("INSERT INTO A (X) VALUES ('GO');");
            var sqlCommandWithSingleLineComment = sqlBuilder.ToString();
            sqlBuilder.Clear();

            // Sql command with a single line comment (with end dashes) containing a GO.
            sqlBuilder.AppendLine("--Single line Comment WITH END comment dashes GO --");
            sqlBuilder.AppendLine("INSERT INTO A (go) VALUES ('Go');");

            var sqlCommandWithSingleLineCommentWithEndDashes = sqlBuilder.ToString();
            sqlBuilder.Clear();

            // Sql command with a single line comment (with end dashes) containing a GO.
            sqlBuilder.AppendLine("INSERT INTO TABLE [Foo] ([Text)");
            sqlBuilder.AppendLine("VALUES (N'Some text. /*Strangely Emphasised Text*/ More text')");

            var strangeInsert = sqlBuilder.ToString();
            sqlBuilder.Clear();

            // Combine into one SQL statement seperated with GO.          
            sqlBuilder.AppendLine(sqlCommandWithMultiLineComment);
            sqlBuilder.AppendLine(sqlGo);
            sqlBuilder.AppendLine(sqlCommandWithSingleLineComment);
            sqlBuilder.AppendLine(sqlGoWithTerminator);
            sqlBuilder.AppendLine(sqlCommandWithSingleLineCommentWithEndDashes);
            sqlBuilder.AppendLine(sqlGo);
            sqlBuilder.AppendLine(strangeInsert);

            var sqlText = sqlBuilder.ToString();
            Console.WriteLine("===== Splitting the following SQL =============");
            Console.WriteLine(sqlText);
            Console.WriteLine("===============================================");

            var commands = sut.SplitScriptIntoCommands(sqlText).ToArray();

            var sqlCommands = commands;
            foreach (var item in sqlCommands)
            {
                Console.WriteLine("=========== Parsed Command ============");
                Console.WriteLine(item);
                Console.WriteLine("=======================================");
            }
            sqlCommands.ShouldNotBeNull();
            sqlCommands.Length.ShouldBe(4);

            sqlCommands[0].ShouldBe(sqlCommandWithMultiLineComment.Trim());
            sqlCommands[1].ShouldBe(sqlCommandWithSingleLineComment.Trim());
            sqlCommands[2].ShouldBe(sqlCommandWithSingleLineCommentWithEndDashes.Trim());
            sqlCommands[3].ShouldBe(strangeInsert.Trim());
        }
    }
}
