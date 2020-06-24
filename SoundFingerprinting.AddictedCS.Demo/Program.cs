using SoundFingerprinting.Audio;
using SoundFingerprinting.Builder;
using SoundFingerprinting.Configuration;
using SoundFingerprinting.DAO.Data;
using SoundFingerprinting.Data;
using SoundFingerprinting.InMemory;
using System;
using System.Threading.Tasks;

namespace SoundFingerprinting.AddictedCS.Demo
{
    class Program
    {
        static IModelService modelService = new InMemoryModelService(); // store fingerprints in RAM
        static IAudioService audioService = new SoundFingerprintingAudioService(); // default audio library

        static async Task Main(string[] args)
        {
            await StoreForLaterRetrieval("./Emma-EmailSupport.wav", "email");
            await StoreForLaterRetrieval("./Emma-FTP.wav", "ftp");
            await StoreForLaterRetrieval("./Emma-Hardware-Support.wav", "hardware");

            var foundTrack = await GetBestMatchForSong("./custom3.wav");
            Console.WriteLine(foundTrack?.Id);
        }

        public static async Task<TrackData> GetBestMatchForSong(string queryAudioFile)
        {
            int secondsToAnalyze = 3; // number of seconds to analyze from query file
            int startAtSecond = 0; // start at the begining

            // query the underlying database for similar audio sub-fingerprints
            var queryResult = await QueryCommandBuilder.Instance.BuildQueryCommand()
                                                 .From(queryAudioFile, secondsToAnalyze, startAtSecond)
                                                 .WithQueryConfig(new HighPrecisionQueryConfiguration())
                                                 .UsingServices(modelService, audioService)
                                                 .Query();

            return queryResult.BestMatch?.Track;
        }

        public static async Task StoreForLaterRetrieval(string pathToAudioFile, string trackInfo)
        {
            var track = new TrackInfo(trackInfo, trackInfo, trackInfo);

            // create fingerprints
            var hashedFingerprints = await FingerprintCommandBuilder.Instance
                                        .BuildFingerprintCommand()
                                        .From(pathToAudioFile)
                                        .WithFingerprintConfig(new HighPrecisionFingerprintConfiguration())
                                        .UsingServices(audioService)
                                        .Hash();

            // store hashes in the database for later retrieval
            modelService.Insert(track, hashedFingerprints);
        }
    }
}
