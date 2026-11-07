using System;
using Shouldly;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Watching;
using Xunit;

namespace SqlSource.Tool.Tests.Watching;

public sealed class RepositoryFoldersTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void FoldersOf_InsideARepository_AreTheFolderUpToTheRoot()
    {
        _ = _folder.CreateFolder("repo/.git");

        RepositoryFolders
            .FoldersOf(_folder.PathOf("repo/src/app"))
            .ShouldBe([_folder.PathOf("repo/src/app"), _folder.PathOf("repo/src"), _folder.PathOf("repo")]);
    }

    [Fact]
    public void FoldersOf_GitFile_MarksTheRootOfAWorktree()
    {
        _ = _folder.WriteFile("worktree/.git", "gitdir: /elsewhere\n");

        RepositoryFolders
            .FoldersOf(_folder.PathOf("worktree/src"))
            .ShouldBe([_folder.PathOf("worktree/src"), _folder.PathOf("worktree")]);
        RepositoryFolders.IsInRepository(_folder.PathOf("worktree/src")).ShouldBeTrue();
    }
}
