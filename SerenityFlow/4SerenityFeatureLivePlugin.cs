using GitCommands;
using GitUIPluginInterfaces;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SerenityFlow
{
    public class SerenityFeatureLivePlugin : GitPluginBase, IGitPluginForRepository, IGitPlugin
    {
        public override string Description
        {
            get
            {
                return "4) FEATURE'ı MASTER'a BİRLEŞTİR (Tag/Merge-Squash)";
            }
        }

        public override bool Execute(GitUIBaseEventArgs args)
        {
            var module = (GitModule)args.GitModule;
            var allChangesCmd = GitCommands.GitCommandHelpers.GetAllChangedFilesCmd(true, UntrackedFilesMode.All, IgnoreSubmodulesMode.All);;

            int exitCode;
            exitCode = args.GitModule.RunGitCmdResult("submodule update --init --recursive").ExitCode;
            
            var statusString = args.GitModule.RunGit(allChangesCmd, out exitCode);
            var changedFiles = GitCommandHelpers.GetAllChangedFilesFromString(module, statusString);
            if (changedFiles.Count != 0)
            {
                MessageBox.Show("Commit edilmeyi bekleyen dosyalarınız var. Lütfen işlemden önce bu dosyaları commit ediniz!");
                return false;
            }

            var currentBranch = (args.GitModule.GetSelectedBranch() ?? "").ToLowerInvariant();

            // master branch inde değilsek ona geçelim
            var branch = currentBranch;
            if (branch != "master")
            {
                var switchBranchCmd = GitCommandHelpers.CheckoutCmd("master", LocalChangesAction.DontChange);
                args.GitModule.RunGit(switchBranchCmd, out exitCode);

                branch = args.GitModule.GetSelectedBranch().ToLowerInvariant();
                if (branch != "master")
                {
                    MessageBox.Show("Master branch'ine geçiş yapılamadı. İşleme devam edilemiyor!");
                    return false;
                }
            }

            // master a direk commit olmadığını varsayıyoruz, dolayısıyla rebase e gerek yok, sadece pull yapalım.
            // rebase yaparsak bazı merge commitleriyle ilgili kayıp yaşanabilir
            // bu arada eğer local te bir şekilde master da commit varsa (merkezde olmayan??) branch bir önceki committen alınmış olur
            var pullCmd = module.PullCmd("origin", "master", "master", false);
            var pullResult = args.GitModule.RunGit(pullCmd, out exitCode);

            if (exitCode != 0)
            {
                MessageBox.Show("Pull işlemi esnasında şu hata alındı:\n" +
                    pullResult + "\nExitCode:" + exitCode);
                return true;
            }

            currentBranch = (args.GitModule.GetSelectedBranch() ?? "").ToLowerInvariant();
            if (currentBranch.IsNullOrEmpty() || currentBranch.ToLowerInvariant() != "master")
            {
                MessageBox.Show("Bu işlem master branch'inde yapılmalıdır!");
                return false;
            }

            var featureMergeHash = Clipboard.GetText();

            if (Dialogs.Prompt("Canlıya gönderilecek feature branch'in TEST'e birleştirildiği commit hash'ini giriniz", 
                "Feature Branch Merge", ref featureMergeHash) != DialogResult.OK)
                return false;

            var mergeInfo = module.ShowSha1(featureMergeHash);
            var mergeLines = mergeInfo.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (mergeLines.Length < 3 || 
                !mergeLines[0].StartsWith("commit ", StringComparison.OrdinalIgnoreCase) ||
                !mergeLines[1].StartsWith("Merge: ", StringComparison.OrdinalIgnoreCase) ||
                !mergeLines[2].StartsWith("Author: ", StringComparison.OrdinalIgnoreCase) ||
                !mergeLines[4].Trim().StartsWith("Merge branch '", StringComparison.OrdinalIgnoreCase) ||
                mergeLines[4].IndexOf("' into", StringComparison.OrdinalIgnoreCase) < 0)
            {
                Dialogs.Alert("Merge commit'i yerine aşağıdaki sonuç bulundu:\n\n" +
                    mergeInfo);
                return false;
            }

            var mergeTitle = mergeLines[4].Trim();
            mergeTitle = mergeTitle.Substring("Merge branch '".Length);
            var mergeIntoIdx = mergeTitle.IndexOf("' into");
            var branchName = mergeTitle.Substring(0, mergeIntoIdx);

            var info = module.ShowSha1(featureMergeHash + "^2");
            var lines = info.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 3 || 
                ((!lines[0].StartsWith("commit ", StringComparison.OrdinalIgnoreCase) || 
                  !lines[1].StartsWith("Author: ", StringComparison.OrdinalIgnoreCase)) &&
                 (!lines[0].StartsWith("commit ", StringComparison.OrdinalIgnoreCase) ||
                  !lines[1].StartsWith("Merge: ", StringComparison.OrdinalIgnoreCase) ||
                  !lines[2].StartsWith("Author: ", StringComparison.OrdinalIgnoreCase) ||
                  info.Contains("' into test") ||
                  info.Contains("' into 'test"))))
            {
                Dialogs.Alert("Birleştirilen feature branch'i yerine aşağıdaki sonuç bulundu:\n\n" +
                    info);
                return false;
            }

            var author = lines[1].Trim().Substring("Author: ".Length).Trim();
            var commit = lines[0].Trim().Substring("commit ".Length).Trim();

            var tagName = "published-" + branchName;
            var tagResult = module.Tag(tagName, commit, false, false);
            if (!tagResult.IsNullOrWhiteSpace())
                Dialogs.Alert("TAG RESULT: " + tagResult);

            var pushTagCmd = GitCommandHelpers.PushTagCmd("origin", tagName, false);
            var pushTagResult = args.GitModule.RunGit(pushTagCmd, out exitCode);

            if (exitCode != 0)
            {
                MessageBox.Show("Tag push işlemi esnasında şu hata alındı:\n" + pushTagResult + "\nExitCode:" + exitCode);
                return true;
            }

            var mergeCmd = GitCommandHelpers.MergeBranchCmd(commit, true, true, true, null);
            var mergeResult = args.GitModule.RunGit(mergeCmd, out exitCode);

            if (exitCode != 0 && exitCode != 128)
            {
                MessageBox.Show("Merge işlemi esnasında şu hata alındı:\n" + mergeResult + "\nExitCode:" + exitCode);
                return true;
            }

            var gitDirectory = args.GitModule.GetGitDirectory();
            var msg = File.ReadAllText(Path.Combine(gitDirectory, "SQUASH_MSG"));
            msg = "Publish Branch '" + branchName + "'\n\n" + msg;
            var msgFile = Path.Combine(gitDirectory, "SQUASH_MSG2");
            File.WriteAllText(msgFile, msg);
            try
            {
                var commitCmd = "commit --author=\"" + author + "\" --file=\"" + msgFile.Replace("\\", "/") + "\"";
                var commitResult = args.GitModule.RunGit(commitCmd, out exitCode);
                
                if (exitCode != 0)
                {
                    MessageBox.Show("Commit işlemi esnasında şu hata alındı:\n" + mergeResult + "\nExitCode:" + exitCode);
                    return true;
                }
            }
            finally
            {
                File.Delete(msgFile);
            }
            
            MessageBox.Show(String.Format("{0} feature branch'i başarıyla MASTER'a merge edildi.\n\nDeğişiklikleri inceleyip sürüm çıkabilirsiniz.", branchName));

            return true;
        }
    }
}