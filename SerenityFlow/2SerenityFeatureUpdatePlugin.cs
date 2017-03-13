using System;
using System.Linq;
using System.Windows.Forms;
using GitCommands;
using GitUIPluginInterfaces;
using ResourceManager;

namespace SerenityFlow
{

    public class SerenityFeatureUpdatePlugin : GitPluginBase, IGitPluginForRepository, IGitPlugin
    {
        public SerenityFeatureUpdatePlugin()
        {
            SetNameAndDescription("2) Canlıdan Değişiklikleri Al (master'dan MERGE)");
            Translate();
        }

        public override bool Execute(GitUIBaseEventArgs args)
        {
            var module = (GitModule)args.GitModule;
            var allChangesCmd = GitCommands.GitCommandHelpers.GetAllChangedFilesCmd(true, UntrackedFilesMode.All, IgnoreSubmodulesMode.All);

            int exitCode;
            exitCode = args.GitModule.RunGitCmdResult("submodule update --init --recursive").ExitCode;

            // öncelikle bekleyen hiçbir değişiklik olmadığından emin oluyoruz
            var statusStringX = args.GitModule.RunGitCmdResult(allChangesCmd);
            var changedFiles = GitCommandHelpers.GetAllChangedFilesFromString(module, statusStringX.StdOutput);
            if (changedFiles.Count != 0)
            {
                MessageBox.Show("Commit edilmeyi bekleyen dosyalarınız var. Lütfen işlemden önce bu dosyaları commit ediniz!");
                return false;
            }

            // bunun bir feature branch i olmasını kontrol et
            var featureBranch = (args.GitModule.GetSelectedBranch() ?? "").ToLowerInvariant();
            if (featureBranch.IsNullOrEmpty() || featureBranch == "master" || featureBranch == "test")
            {
                MessageBox.Show("Bu işlem master ya da test branch lerinde yapılamaz!");
                return false;
            }

            // origin deki son değişikliklerden haberdar ol
            var fetchCmd = module.FetchCmd("origin", "", "");
            var cmdResult = args.GitModule.RunGitCmdResult(fetchCmd);
            var fetchResult = cmdResult.StdError;
            exitCode = cmdResult.ExitCode;

            if (exitCode != 0)
            {
                MessageBox.Show("Fetch işlemi esnasında şu hata alındı:\n" +
                    fetchResult + "\nExitCode:" + exitCode);
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

            var switchBranchCmd = GitCommandHelpers.CheckoutCmd("master", LocalChangesAction.DontChange);
            exitCode = args.GitModule.RunGitCmdResult(switchBranchCmd).ExitCode;

            var currentBranch = args.GitModule.GetSelectedBranch().ToLowerInvariant();
            if (currentBranch != "master")
            {
                MessageBox.Show("Master branch'ine geçiş yapılamadı. İşleme devam edilemiyor!");
                return true;
            }

            var pullCmd = module.PullCmd("origin", "master", "master", false);
            cmdResult = args.GitModule.RunGitCmdResult(pullCmd);
            var pullResult = cmdResult.StdError;
            exitCode = cmdResult.ExitCode;

            if (exitCode != 0)
            {
                MessageBox.Show("Pull işlemi esnasında şu hata alındı:\n" +
                    pullResult + "\nExitCode:" + exitCode);
                return true;
            }

            switchBranchCmd = GitCommandHelpers.CheckoutCmd(featureBranch, LocalChangesAction.DontChange);
            exitCode = args.GitModule.RunGitCmdResult(switchBranchCmd).ExitCode;

            currentBranch = args.GitModule.GetSelectedBranch();
            if (currentBranch != featureBranch)
            {
                MessageBox.Show("Feature branch'ine geri geçiş yapılamadı. İşleme devam edilemiyor!");
                return true;
            }

            // master'ı feature branch e birleştir
            var mergeCmd = GitCommandHelpers.MergeBranchCmd("master", allowFastForward: true, squash: false, noCommit: false, strategy: "");
            cmdResult = args.GitModule.RunGitCmdResult(mergeCmd);
            var mergeResult = cmdResult.StdError;
            exitCode = cmdResult.ExitCode;

            if (exitCode != 0)
            {
                MessageBox.Show("Merge işlemi esnasında şu hata alındı:\n" +
                    mergeResult + "\nExitCode:" + exitCode);
                return true;
            }

            if (remoteBranchExists)
            {
                // varsa local deki değişikliği hemen merkeze gönderelim
                var pushFeatureCmd = GitCommandHelpers.PushTagCmd("origin", featureBranch, false);
                cmdResult = args.GitModule.RunGitCmdResult(pushFeatureCmd);
                var pushFeatureResult = cmdResult.StdError;
                exitCode = cmdResult.ExitCode;


                if (exitCode != 0)
                {
                    MessageBox.Show("Push feature işlemi esnasında şu hata alındı:\n" +
                        pushFeatureResult + "\nExitCode:" + exitCode);
                    return true;
                }
            }

            MessageBox.Show(String.Format("{0} feature branch'i başarıyla master'daki değişiklikler ile güncellendi.", featureBranch));

            return true;
        }
    }
}