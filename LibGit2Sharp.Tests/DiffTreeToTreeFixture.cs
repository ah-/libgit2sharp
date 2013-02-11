﻿using System;
using System.IO;
        [Fact]
        public void CanHandleTwoTreeEntryChangesWithTheSamePath()
        {
            SelfCleaningDirectory scd = BuildSelfCleaningDirectory();

            using (Repository repo = Repository.Init(scd.DirectoryPath))
            {
                Blob mainContent = CreateBlob(repo, "awesome content\n");
                Blob linkContent = CreateBlob(repo, "../../objc/Nu.h");

                string path = string.Format("include{0}Nu{0}Nu.h", Path.DirectorySeparatorChar);

                var tdOld = new TreeDefinition()
                    .Add(path, linkContent, Mode.SymbolicLink)
                    .Add("objc/Nu.h", mainContent, Mode.NonExecutableFile);

                Tree treeOld = repo.ObjectDatabase.CreateTree(tdOld);

                var tdNew = new TreeDefinition()
                    .Add(path, mainContent, Mode.NonExecutableFile);

                Tree treeNew = repo.ObjectDatabase.CreateTree(tdNew);

                TreeChanges changes = repo.Diff.Compare(treeOld, treeNew);

                /*
                 * $ git diff-tree -p 5c87b67 d5278d0
                 * diff --git a/include/Nu/Nu.h b/include/Nu/Nu.h
                 * deleted file mode 120000
                 * index 19bf568..0000000
                 * --- a/include/Nu/Nu.h
                 * +++ /dev/null
                 * @@ -1 +0,0 @@
                 * -../../objc/Nu.h
                 * \ No newline at end of file
                 * diff --git a/include/Nu/Nu.h b/include/Nu/Nu.h
                 * new file mode 100644
                 * index 0000000..f9e6561
                 * --- /dev/null
                 * +++ b/include/Nu/Nu.h
                 * @@ -0,0 +1 @@
                 * +awesome content
                 * diff --git a/objc/Nu.h b/objc/Nu.h
                 * deleted file mode 100644
                 * index f9e6561..0000000
                 * --- a/objc/Nu.h
                 * +++ /dev/null
                 * @@ -1 +0,0 @@
                 * -awesome content
                 */

                Assert.Equal(1, changes.Deleted.Count());
                Assert.Equal(0, changes.Modified.Count());
                Assert.Equal(1, changes.TypeChanged.Count());

                TreeEntryChanges change = changes[path];
                Assert.Equal(Mode.SymbolicLink, change.OldMode);
                Assert.Equal(Mode.NonExecutableFile, change.Mode);
                Assert.Equal(ChangeKind.TypeChanged, change.Status);
                Assert.Equal(path, change.Path);
            }
        }

        private static Blob CreateBlob(Repository repo, string content)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            using (var binReader = new BinaryReader(stream))
            {
                return repo.ObjectDatabase.CreateBlob(binReader);
            }
        }

        [Fact]
        [Fact]

        [Fact]
        public void ComparingReliesOnProvidedConfigEntriesIfAny()
        {
            TemporaryCloneOfTestRepo path = BuildTemporaryCloneOfTestRepo(StandardTestRepoWorkingDirPath);

            const string file = "1/branch_file.txt";

            using (var repo = new Repository(path.DirectoryPath))
            {
                TreeEntry entry = repo.Head[file];
                Assert.Equal(Mode.ExecutableFile, entry.Mode);

                // Recreate the file in the workdir without the executable bit
                string fullpath = Path.Combine(repo.Info.WorkingDirectory, file);
                File.Delete(fullpath);
                File.WriteAllBytes(fullpath, ((Blob)(entry.Target)).Content);

                // Unset the local core.filemode, if any.
                repo.Config.Unset("core.filemode", ConfigurationLevel.Local);
            }

            SelfCleaningDirectory scd = BuildSelfCleaningDirectory();

            var options = BuildFakeSystemConfigFilemodeOption(scd, true);

            using (var repo = new Repository(path.DirectoryPath, options))
            {
                TreeChanges changes = repo.Diff.Compare(new []{ file });

                Assert.Equal(1, changes.Count());

                var change = changes.Modified.Single();
                Assert.Equal(Mode.ExecutableFile, change.OldMode);
                Assert.Equal(Mode.NonExecutableFile, change.Mode);
            }

            options = BuildFakeSystemConfigFilemodeOption(scd, false);

            using (var repo = new Repository(path.DirectoryPath, options))
            {
                TreeChanges changes = repo.Diff.Compare(new[] { file });

                Assert.Equal(0, changes.Count());
            }
        }

        private RepositoryOptions BuildFakeSystemConfigFilemodeOption(
            SelfCleaningDirectory scd,
            bool value)
        {
            Directory.CreateDirectory(scd.DirectoryPath);

            var options = new RepositoryOptions
                              {
                                  SystemConfigurationLocation = Path.Combine(
                                      scd.RootedDirectoryPath, "fake-system.config")
                              };

            StringBuilder sb = new StringBuilder()
                .AppendFormat("[core]{0}", Environment.NewLine)
                .AppendFormat("filemode = {1}{0}", Environment.NewLine, value);
            File.WriteAllText(options.SystemConfigurationLocation, sb.ToString());

            return options;
        }