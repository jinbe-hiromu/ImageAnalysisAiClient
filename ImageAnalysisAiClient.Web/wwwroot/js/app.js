(() => {
    const CAPTURE_INTERVAL_MS = 500;
    const CAPTURE_DURATION_MS = 3000;

    const cmbProvider = document.getElementById('cmbProvider');
    const cmbModel = document.getElementById('cmbModel');
    const btnSelectImage = document.getElementById('btnSelectImage');
    const btnSend = document.getElementById('btnSend');
    const lblImagePath = document.getElementById('lblImagePath');
    const txbAdditionalInfo = document.getElementById('txbAdditionalInfo');
    const video = document.getElementById('video');
    const picImage = document.getElementById('picImage');
    const canvas = document.getElementById('canvas');
    const results = document.getElementById('results');
    const title = document.getElementById('title');

    let stream = null;
    let capturedImages = [];
    let conversationHistory = [];
    let resultMarkdown = '';
    let isCapturing = false;
    let isSending = false;

    function updateCaptureStatus(status) {
        lblImagePath.textContent = `取得枚数: ${capturedImages.length}枚 - ${status}`;
    }

    function updateButtons() {
        btnSelectImage.disabled = isCapturing || isSending;
        btnSend.disabled = isCapturing || isSending || capturedImages.length === 0;
    }

    async function renderMarkdown(pending = '') {
        try {
            const response = await fetch('api/markdown', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ markdown: resultMarkdown + pending })
            });
            results.innerHTML = await response.text();
            results.scrollTop = results.scrollHeight;
        } catch {
            results.textContent = resultMarkdown + pending;
        }
    }

    function setResultMarkdown(markdown) {
        resultMarkdown = markdown;
        return renderMarkdown();
    }

    function appendResultMarkdown(markdown) {
        resultMarkdown += markdown;
        return renderMarkdown();
    }

    async function loadModels() {
        cmbModel.innerHTML = '';
        title.textContent = 'ImageAnalysisAiClient';
        try {
            const response = await fetch(`api/models?provider=${encodeURIComponent(cmbProvider.value)}`);
            if (!response.ok) {
                const problem = await response.json().catch(() => null);
                throw new Error(problem?.detail ?? response.statusText);
            }

            const models = await response.json();
            for (const name of models) {
                cmbModel.add(new Option(name, name));
            }

            if (models.length === 0) {
                await setResultMarkdown('利用可能なモデルが見つかりませんでした。Ollama が起動しているか、モデルが pull 済みか確認してください。');
            }
            onModelChanged();
        } catch (e) {
            await setResultMarkdown(`モデル一覧の取得に失敗しました: ${e.message}`);
        }
    }

    function onModelChanged() {
        if (cmbModel.value) {
            title.textContent = `ImageAnalysisAiClient - ${cmbModel.value}`;
        }
    }

    async function initializeCamera() {
        if (stream) {
            return;
        }

        if (!navigator.mediaDevices?.getUserMedia) {
            throw new Error('このブラウザーではカメラを利用できません。HTTPS または localhost でアクセスしてください。');
        }

        updateCaptureStatus('カメラを準備中...');
        stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
        video.srcObject = stream;
        await video.play();
        updateCaptureStatus('停止中');
    }

    function captureImage() {
        if (!video.videoWidth) {
            throw new Error('カメラから画像を取得できませんでした。');
        }

        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        canvas.getContext('2d').drawImage(video, 0, 0);
        const dataUrl = canvas.toDataURL('image/png');
        capturedImages.push(dataUrl.substring(dataUrl.indexOf(',') + 1));
        picImage.src = dataUrl;
    }

    async function startImageCapture() {
        if (isCapturing || isSending) {
            return;
        }

        conversationHistory = [];
        await setResultMarkdown('');
        isCapturing = true;
        updateButtons();
        const startedAt = performance.now();

        try {
            await initializeCamera();
            capturedImages = [];
            picImage.removeAttribute('src');
            captureImage();
            const elapsed = Math.round(performance.now() - startedAt);
            updateCaptureStatus(`取得中（500ms間隔、開始から初回画像まで${elapsed}ms）`);

            const captureStartedAt = performance.now();
            await new Promise((resolve, reject) => {
                const timer = setInterval(() => {
                    try {
                        if (performance.now() - captureStartedAt >= CAPTURE_DURATION_MS) {
                            clearInterval(timer);
                            resolve();
                            return;
                        }
                        captureImage();
                        updateCaptureStatus('取得中（500ms間隔）');
                    } catch (e) {
                        clearInterval(timer);
                        reject(e);
                    }
                }, CAPTURE_INTERVAL_MS);
            });
            updateCaptureStatus('撮影完了');
        } catch (e) {
            updateCaptureStatus('撮影停止（取得済み画像を保持）');
            await setResultMarkdown(`カメラ撮影に失敗しました: ${e.message}`);
        } finally {
            isCapturing = false;
            updateButtons();
        }
    }

    async function send() {
        if (isCapturing) {
            await setResultMarkdown('撮影が完了するまでお待ちください。');
            return;
        }

        const additionalInfo = txbAdditionalInfo.value.trim();
        if (capturedImages.length === 0) {
            await setResultMarkdown('開始ボタンを押して画像を取得してください。');
            return;
        }

        const isInitialMessage = conversationHistory.length === 0;
        if (!isInitialMessage && !additionalInfo) {
            await setResultMarkdown('再判定するための補足情報を入力してください。');
            return;
        }

        if (!cmbModel.value) {
            await setResultMarkdown('モデルを選択してください。');
            return;
        }

        isSending = true;
        updateButtons();
        txbAdditionalInfo.disabled = true;

        let displayPrompt = isInitialMessage ? '撮影した画像の判定を依頼しました。' : additionalInfo;
        if (isInitialMessage && additionalInfo) {
            displayPrompt += `\n補足情報: ${additionalInfo}`;
        }
        await appendResultMarkdown(`\n### あなた\n\n${displayPrompt}\n\n### 判定\n\n`);

        let assistantResponse = '';
        let succeeded = false;
        try {
            const response = await fetch('api/analyze', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    provider: cmbProvider.value,
                    model: cmbModel.value,
                    additionalInfo,
                    images: isInitialMessage ? capturedImages : [],
                    history: conversationHistory
                })
            });

            if (!response.ok) {
                const text = await response.text();
                let message = text;
                try { message = JSON.parse(text).detail ?? text; } catch { }
                throw new Error(message || response.statusText);
            }

            const reader = response.body.getReader();
            const decoder = new TextDecoder();
            let lastRender = performance.now();
            while (true) {
                const { done, value } = await reader.read();
                if (done) {
                    break;
                }
                assistantResponse += decoder.decode(value, { stream: true });
                if (performance.now() - lastRender >= 500) {
                    await renderMarkdown(assistantResponse);
                    lastRender = performance.now();
                }
            }
            assistantResponse += decoder.decode();

            const errorIndex = assistantResponse.indexOf('[[ERROR]]');
            if (errorIndex >= 0) {
                throw new Error(assistantResponse.substring(errorIndex + '[[ERROR]]'.length).replace(/^エラー: /, ''));
            }
            succeeded = true;
        } catch (e) {
            await appendResultMarkdown(`エラー: ${e.message}\n`);
        } finally {
            if (succeeded) {
                let userPrompt = additionalInfo;
                if (isInitialMessage) {
                    userPrompt = `以下は500ms間隔で取得した連続画像です（全${capturedImages.length}枚）。すべての画像を確認し、同じ物体が複数画像に写っている場合は重複して数えずに分別してください。`;
                    if (additionalInfo) {
                        userPrompt += `\n\nユーザーからのゴミに関する補足情報:\n${additionalInfo}`;
                    }
                }
                conversationHistory.push({ role: 'user', text: userPrompt });
                conversationHistory.push({ role: 'assistant', text: assistantResponse });
                txbAdditionalInfo.value = '';
                await appendResultMarkdown(`${assistantResponse}\n\n`);
            }

            txbAdditionalInfo.disabled = false;
            isSending = false;
            updateButtons();
        }
    }

    cmbProvider.addEventListener('change', loadModels);
    cmbModel.addEventListener('change', onModelChanged);
    btnSelectImage.addEventListener('click', startImageCapture);
    btnSend.addEventListener('click', send);

    updateButtons();
    loadModels();
    initializeCamera().catch(e => {
        updateCaptureStatus('カメラを準備できませんでした。開始ボタンで再試行できます');
        setResultMarkdown(`カメラの準備に失敗しました: ${e.message}`);
    });
})();
