using System;
using System.Linq;
using System.Windows.Forms;
using GitCommands;
using GitUIPluginInterfaces;

namespace SerenityFlow
{
    public class SerenityFeatureTestPlugin : GitPluginBase, IGitPluginForRepository, IGitPlugin
    {
        public override string Description
        {
            get
            {
                return "3) TEST'e Gönder!";
            }
        }

        public override bool Execute(GitUIBaseEventArgs args)
        {
            var module = (GitModule)args.GitModule;
            var allChangesCmd = GitCommandHelpers.GetAllChangedFilesCmd(true, UntrackedFilesMode.All, IgnoreSubmodulesMode.All);;
            CmdResult cmdResult;
            var exitCode = args.GitModule.RunGitCmdResult("submodule update --init --recursive").ExitCode;

            // öncelikle bekleyen hiçbir değişiklik olmadığından emin oluyoruz
            var status = args.GitModule.RunGitCmdResult(allChangesCmd);
            var statusString = status.StdOutput;
            exitCode = status.ExitCode;
            var changedFiles = GitCommandHelpers.GetAllChangedFilesFromString(module, statusString);
            if (changedFiles.Count != 0)
            {
                MessageBox.Show("Commit edilmeyi bekleyen dosyalarınız var. Lütfen TEST işleminden önce bu dosyaları commit ediniz!");
                return false;
            }

            // bunun bir feature branch i olmasını kontrol et
            var featureBranch = (args.GitModule.GetSelectedBranch() ?? "").ToLowerInvariant();
            if (featureBranch.IsNullOrEmpty() || featureBranch == "master" || featureBranch == "test")
            {
                MessageBox.Show("Bu işlem master ya da test branch lerinde yapılamaz!");
                return false;
            }

            // son kez onay alalım
            if (!Dialogs.Confirm("Bulunduğunuz branch, TEST'e gönderilecek.\n\n" +
                "Devam etmek istiyor musunuz?"))
                return true;

            // origin deki son değişikliklerden haberdar ol
            var fetchCmd = module.FetchCmd("origin", "", "");
            var fetchResultX = args.GitModule.RunGitCmdResult(fetchCmd);
            exitCode = fetchResultX.ExitCode;
            if (exitCode != 0)
            {
                MessageBox.Show("Fetch işlemi esnasında şu hata alındı:\n" +
                    fetchResultX.StdError + "\nExitCode:" + exitCode);
                return false;
            }

            // remote branch varsa, son değişiklikleri pull edelim
            var remoteBranchExists = module.GetRefs(false, true).Any(x => x.Name == featureBranch & x.IsRemote);
            if (remoteBranchExists)
            {
                var pullFeatureCmd = module.PullCmd("origin", featureBranch, featureBranch, false);
                cmdResult = args.GitModule.RunGitCmdResult(pullFeatureCmd);
                var pullFeatureResult = cmdResult.StdError;
                exitCode = cmdResult.ExitCode;

                if (exitCode != 0)
                {
                    MessageBox.Show("Feature pull işlemi esnasında şu hata alındı:\n" +
                        pullFeatureResult + "\nExitCode:" + exitCode);
                    return true;
                }
            }

            // test branch ine geçiş yap
            var switchBranchCmd = GitCommandHelpers.CheckoutCmd("test", LocalChangesAction.DontChange);
            exitCode = args.GitModule.RunGitCmdResult(switchBranchCmd).ExitCode;

            var currentBranch = args.GitModule.GetSelectedBranch().ToLowerInvariant();
            if (currentBranch != "test")
            {
                MessageBox.Show("Test branch'ine geçiş yapılamadı. İşleme devam edilemiyor!");
                return true;
            }

            // varsa test teki son değişiklikleri al
            // test e direk commit olmayacağı varsayıldığından rebase e gerek yok.
            var pullCmd = module.PullCmd("origin", "test", "test", false);
            cmdResult = args.GitModule.RunGitCmdResult(pullCmd);
            var pullResult = cmdResult.StdError;
            exitCode = cmdResult.ExitCode;

            if (exitCode != 0)
            {
                MessageBox.Show("Test'ten pull işlemi esnasında şu hata alındı:\n" +
                    pullResult + "\nExitCode:" + exitCode);
                return true;
            }

            // feature branch i test e birleştir
            var mergeCmd = GitCommandHelpers.MergeBranchCmd(featureBranch, allowFastForward: false, squash: false, noCommit: false, strategy: "");
            cmdResult = args.GitModule.RunGitCmdResult(mergeCmd);
            var mergeResult = cmdResult.StdError;
            exitCode = cmdResult.ExitCode;
            
            if (exitCode != 0)
            {
                MessageBox.Show("Merge işlemi esnasında şu hata alındı:\n" +
                    mergeResult + "\nExitCode:" + exitCode);
                return true;
            }

            MessageBox.Show(String.Format("{0} feature branch'i başarıyla TEST'e merge edildi.\n\nLütfen projeyi son haliyle TEST'teyken " +
                "build edip, PUSH işlemi yapınız.", featureBranch));

            Clipboard.SetText("Gerekli kontroller yapılmıştır, gerçek ortama çıkarılabilir mi?");

            return true;
        }
    }
}
