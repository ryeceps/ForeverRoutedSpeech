#include "whisper.h"
#include <exception>
#include "fasttext.h"
#include <atomic>
#include <algorithm>
#include <cmath>
#include <cstring>
#include <iomanip>
#include <locale>
#include <memory>
#include <sstream>
#include <string>
#include <vector>

#ifdef _WIN32
#define EXPORT extern "C" __declspec(dllexport)
#else
#define EXPORT extern "C" __attribute__((visibility("default")))
#endif
struct Engine {
    whisper_context* speech = nullptr;
    std::unique_ptr<fasttext::FastText> router;
    std::atomic<bool> cancelled{false};
    int threads = 1;
    ~Engine() { if (speech) whisper_free(speech); }
};
static int copy_text(const std::string& text, char* output, int capacity) {
    if (!output || capacity <= 0 || text.size() >= static_cast<size_t>(capacity)) return -3;
    std::memcpy(output, text.c_str(), text.size()+1);
    return static_cast<int>(text.size());
}
EXPORT void* wvr_create(const char* speech_model, const char* router_model, int threads) {
    try {
        auto engine = std::make_unique<Engine>();
        engine->threads = threads > 0 ? threads : 1;
        auto params = whisper_context_default_params(); params.use_gpu = false;
        // Whisper logs contain model diagnostics, never recognized speech.
        engine->speech = whisper_init_from_file_with_params(speech_model, params);
        if (!engine->speech) return nullptr;
        if (router_model && router_model[0]) {
            engine->router = std::make_unique<fasttext::FastText>(); engine->router->loadModel(router_model);
        }
        return engine.release();
    } catch (...) { return nullptr; }
}
EXPORT int wvr_transcribe(void* handle, const float* samples, int count, char* output, int capacity) {
    auto* engine = static_cast<Engine*>(handle);
    if (!engine || !engine->speech || !samples || count <= 0 || count > 30*16000) return -1;
    try {
        engine->cancelled.store(false);
        double energy = 0; for (int i=0;i<count;i++) { if(!std::isfinite(samples[i])) return -1; energy += samples[i]*samples[i]; }
        if (count < 1600 || std::sqrt(energy/count) < .003) return copy_text("",output,capacity);
        auto params = whisper_full_default_params(WHISPER_SAMPLING_GREEDY);
        params.language = "en"; params.translate = false; params.n_threads = engine->threads;
        params.no_context = true; params.no_timestamps = true;
        params.print_realtime = false; params.print_progress = false;
        params.print_timestamps = false; params.print_special = false;
        params.suppress_blank = true; params.suppress_nst = true;
        params.abort_callback = [](void* user) { return static_cast<Engine*>(user)->cancelled.load(); };
        params.abort_callback_user_data = engine;
        int result = whisper_full(engine->speech,params,samples,count);
        if (engine->cancelled.load()) return -2;
        if (result != 0) return -1;
        std::string text;
        for(int i=0;i<whisper_full_n_segments(engine->speech);i++)
            if(whisper_full_get_segment_no_speech_prob(engine->speech,i)<.6f) text += whisper_full_get_segment_text(engine->speech,i);
        return copy_text(text,output,capacity);
    } catch (...) { return -1; }
}
// SpeakForever owns its Whisper model; this handle loads only the classifier.
EXPORT void* wvr_router_create(const char* router_model) {
    try {
        if (!router_model || !router_model[0]) return nullptr;
        auto engine = std::make_unique<Engine>();
        engine->router = std::make_unique<fasttext::FastText>();
        engine->router->loadModel(router_model);
        return engine.release();
    } catch (...) { return nullptr; }
}
EXPORT int wvr_predict(void* handle, const char* features, char* output, int capacity) {
    auto* engine = static_cast<Engine*>(handle);
    if (!engine || !features) return -1;
    if (!engine->router) return copy_text("",output,capacity);
    try {
        std::istringstream input(features); std::vector<std::pair<fasttext::real,std::string>> predictions;
        engine->router->predictLine(input,predictions,10,0);
        std::ostringstream text; text.imbue(std::locale::classic()); text << std::setprecision(9);
        for (const auto& prediction: predictions) text << prediction.second << '\t' << std::clamp(prediction.first,0.0f,1.0f) << '\n';
        return copy_text(text.str(),output,capacity);
    } catch (...) { return -1; }
}
EXPORT void wvr_cancel(void* handle) { if(handle) static_cast<Engine*>(handle)->cancelled.store(true); }
EXPORT void wvr_destroy(void* handle) { delete static_cast<Engine*>(handle); }
