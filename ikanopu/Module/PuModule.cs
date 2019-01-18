﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ikanopu.Config;
using ikanopu.Service;
using Newtonsoft.Json;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ikanopu.Module {
    [Group("pu"), Alias("ikanopu")]
    public class PuModule : ModuleBase {
        public DiscordSocketClient Discord { get; set; }
        public CommandService CommandService { get; set; }
        public ImageProcessingService ImageProcessingService { get; set; }

        [Command, Summary("コマンド一覧を表示します")]
        [Alias("help")]
        public async Task Help() {
            var sb = new StringBuilder();
            sb.AppendLine("*ikanopu(beta)*");
            sb.AppendLine("プライベートマッチの音声チャンネル遷移を自動でやってくれるかも");
            sb.AppendLine();
            sb.AppendLine("コマンドは先頭に`!`をつけた後に以下リストにあるものが使用できます");
            sb.AppendLine("https://github.com/kamiyaowl/ikanopu/blob/master/ikanopu/Module/PuModule.cs");

            var builder = new EmbedBuilder();
            foreach (var c in CommandService.Commands) {
                builder.AddField(
                    c.Aliases.First() + " " + string.Join(" ", c.Parameters.Select(x => $"[{x}]")),
                    (c.Summary ?? "no description") + "\n" +
                        string.Join("\n", c.Parameters.Select(x => $"[{x.Name}]: {x.Summary}"))
                );
            }
            await ReplyAsync(sb.ToString(), false, builder.Build());
        }

        [Command("detect"), Summary("現在の画面から認識結果を返します")]
        public async Task Capture(
            [Summary("切り出す領域を設定します。`!pu show config CropOptions`で閲覧できます")] int cropIndex = 0,
            [Summary("trueの場合、認識に使用した画像もアップロードします")] bool uploadImage = false
            ) {
            var path = Path.Combine(ImageProcessingService.Config.TemporaryDirectory, "recognize.jpg");
            // とりあえず認識してあげる
            // TODO: index指定をやめて、推測させるところまで移植する

            using (var result = await ImageProcessingService.RecognizeAsync(cropIndex)) {
                Mat mat = null;
                // アップロード用のプレビューを作る
                lock (ImageProcessingService.CaptureRawMat) {
                    mat = ImageProcessingService.CaptureRawMat.Clone();
                }
                result.DrawPreview(mat);
                mat.SaveImage(path);
                mat.Dispose();
                mat = null;
                // あとで登録できるようにpostMatsをローカルに保管する
                foreach (var (sourceMat, i) in result.SourceMats.Select((x, i) => (x, i))) {
                    var p = Path.Combine(ImageProcessingService.Config.TemporaryDirectory, $"recognize-[{i}].bmp");
                    sourceMat.SaveImage(p);
                }
                // 認識結果の埋め込みを作ってあげる
                var builder = new EmbedBuilder();
                foreach (var r in result.RecognizedUsers.OrderBy(x => x.Index)) {
                    builder.AddField($"[{r.Index}] {r.Team}: {r.User.DisplayName}", $"Discord ID: [TODO: here]\nScore: {r.Independency}");
                }
                // 返す
                var message = @"*認識結果*

現在登録されてないユーザは以下のコマンドで登録できます。
`!pu register [Discord IDもしくは表示名] [登録したい名前の横に書かれた数字]`
※認識結果が異なる場合、現在の画像で追加登録することで認識精度を改善できます。(未検証）
";
                if (uploadImage) {
                    await Context.Channel.SendFileAsync(path, message, false, builder.Build());
                } else {
                    await ReplyAsync(message, false, builder.Build());
                }
            }
        }

        [Command("capture"), Summary("現在のキャプチャデバイスの画像を取得します")]
        public async Task Capture() {
            var path = Path.Combine(ImageProcessingService.Config.TemporaryDirectory, "capture.jpg");

            Mat mat = null;
            lock (ImageProcessingService.CaptureRawMat) {
                mat = ImageProcessingService.CaptureRawMat.Clone();
            }
            mat.SaveImage(path);
            mat.Dispose();
            mat = null;

            await Context.Channel.SendFileAsync(path, "capture.jpg");
        }

        [Group("config")]
        public class ConfigModule : ModuleBase {
            public ImageProcessingService ImageProcessingService { get; set; }

            [Command("show"), Summary("config.jsonの内容を表示します")]
            public async Task ShowConfigRaw([Summary("子要素名、`--all`指定するとすべて表示")] string name) {
                string str = null;
                if (name.Equals("--all")) {
                    str = JsonConvert.SerializeObject(ImageProcessingService.Config, Formatting.Indented);
                } else {
                    dynamic config = ImageProcessingService.Config;
                    var deserialized = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(config));
                    str = JsonConvert.SerializeObject(deserialized[name], Formatting.Indented);
                }

                // 2000文字対策
                var TEXT_LENGTH_N = 1900;
                var n = (str.Length / TEXT_LENGTH_N) + 1;
                for (int i = 0; i < n; ++i) {
                    var length = (n - 1 == i) ? str.Length % TEXT_LENGTH_N : TEXT_LENGTH_N;
                    await Context.Channel.SendMessageAsync($"```\n{str.Substring(i * TEXT_LENGTH_N, length)}\n```");
                }
            }

        }

        [Group("debug")]
        public class DebugModule : ModuleBase {

            [Command("echo"), Summary("俺がオウムだ")]
            public async Task Echo([Remainder, Summary("適当なテキスト")] string text) {
                await ReplyAsync($"\u200B{text}");
            }

            [Command("userinfo"), Summary("ユーザー情報を返します")]
            public async Task UserInfo([Summary("(optional) ユーザID及び名前など(@hogehoge, hogehoge#1234, raw_id)。省略した場合は自身の情報")] IUser user = null) {
                var userInfo = user ?? Context.Client.CurrentUser;
                await ReplyAsync($"{userInfo.Username}#{userInfo.Discriminator} (ID: {userInfo.Id})");
            }

            [Command("clean"), Summary("ikanopuのつぶやきをなかったことにする")]
            public async Task Clean([Summary("上限数")] int limit = 100) {
                var messages = await Context.Channel.GetMessagesAsync(limit).FlattenAsync();
                var filtered = messages.Where(x => x.Author.Id == Context.Client.CurrentUser.Id);
                foreach (var m in filtered) {
                    Console.WriteLine($"MessageMessageAsync: {m}");
                    await Context.Channel.DeleteMessageAsync(m);
                }
            }
        }
    }
}
