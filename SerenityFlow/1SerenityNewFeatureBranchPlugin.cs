using System;
using System.Linq;
using System.Windows.Forms;
using GitCommands;
using GitUIPluginInterfaces;
using ResourceManager;

namespace SerenityFlow
{
    public class SerenityNewFeatureBranchPlugin : GitPluginBase, IGitPluginForRepository, IGitPlugin
    {
        public SerenityNewFeatureBranchPlugin()
        {
            SetNameAndDescription("1) Yeni Feature");
            Translate();
        }

        private static string allChangesCmd;

        public override bool Execute(GitUIBaseEventArgs args)
        {
            var module = (GitModule)args.GitModule;

            int exitCode;
            exitCode = args.GitModule.RunGitCmdResult("submodule update --init --recursive").ExitCode;

            allChangesCmd = allChangesCmd ?? GitCommands.GitCommandHelpers.GetAllChangedFilesCmd(true, UntrackedFilesMode.All, IgnoreSubmodulesMode.All);;

            // öncelikle bekleyen hiçbir değişiklik olmadığından emin oluyoruz
            
            var cmdResult = args.GitModule.RunGitCmdResult(allChangesCmd);
            var statusString = cmdResult.StdError;
            exitCode = cmdResult.ExitCode;
            var changedFiles = GitCommandHelpers.GetAllChangedFilesFromString(module, statusString);
            if (changedFiles.Count != 0)
            {
                MessageBox.Show("Commit edilmeyi bekleyen dosyalarınız var. Lütfen yeni bir feature branch oluşturmadan önce bu dosyaları commit ediniz!");
                return false;
            }

            // feature branch i için uygun bir isim alalım. bu master, test, ya da mevcut bir branch ten farklı olmalı
            string featureBranchName = "";
            while (featureBranchName.IsNullOrEmpty())
            {
                if (Dialogs.Prompt("Yeni bir feature branch oluşturulacak. Bunun için master branch'e geçilip, son değişiklikler pull ile alınacak." +
                        "\n\nLütfen oluşturmak istediğiniz branch'e bir isim veriniz", "Yeni Feature Branch Oluşturma", ref featureBranchName) != DialogResult.OK)
                    return false;

                if (featureBranchName != null)
                {
                    featureBranchName = featureBranchName.Trim();
                    if (featureBranchName != null &&
                        module.GetRefs().Any(x => String.Compare(x.Name, featureBranchName, StringComparison.OrdinalIgnoreCase) == 0))
                    {
                        Dialogs.Alert("Girdiğiniz branch ismine sahip başka bir branch var, lütfen başka bir isim giriniz!");
                        featureBranchName = null;
                    }
                }
            }

            // master branch inde değilsek ona geçelim
            var branch = args.GitModule.GetSelectedBranch().ToLowerInvariant();
            if (branch != "master")
            {
                var switchBranchCmd = GitCommandHelpers.CheckoutCmd("master", LocalChangesAction.DontChange);
                exitCode = args.GitModule.RunGitCmdResult(switchBranchCmd).ExitCode;

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
            cmdResult = args.GitModule.RunGitCmdResult(pullCmd);
            var pullResult = cmdResult.StdError;
            exitCode = cmdResult.ExitCode;

            if (exitCode != 0)
            {
                MessageBox.Show("Pull işlemi esnasında şu hata alındı:\n" +
                    pullResult + "\nExitCode:" + exitCode);
                return true;
            }

            // feature branch oluştur
            var checkoutCmd = "checkout -b " + featureBranchName;
            cmdResult = args.GitModule.RunGitCmdResult(checkoutCmd);
            var checkoutResult = cmdResult.StdError;
            exitCode = cmdResult.ExitCode;

            if (exitCode != 0)
            {
                MessageBox.Show("Branch oluşturma işlemi esnasında şu hata alındı:\n" +
                    checkoutResult + "\nExitCode:" + exitCode);
                return true;
            }

            MessageBox.Show(String.Format("{0} feature branch'i başarıyla oluşturuldu.", featureBranchName));

            return true;
        }
    }
}
