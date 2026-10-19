const authView = document.querySelector(".auth-layout");
const authForm = document.querySelector("#auth-form");
const appView = document.querySelector("#dating-app");
const authMessage = document.querySelector("#auth-message");
const authButton = document.querySelector("#auth-submit");
const appState = { token: localStorage.getItem("kiss.accessToken"), profile: null, interests: [] };
let authMode = "login";

const escapeHtml = (value = "") => String(value).replace(/[&<>"']/g, (character) => ({
  "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
}[character]));

function photoUrl(value) {
  return typeof value === "string" && value.startsWith("/uploads/") ? value : "";
}

function ageFrom(value) {
  if (!value) return "";
  const birthDate = new Date(`${value}T00:00:00`);
  if (Number.isNaN(birthDate.getTime())) return "";
  const today = new Date();
  let age = today.getFullYear() - birthDate.getFullYear();
  if (today.getMonth() < birthDate.getMonth() || (today.getMonth() === birthDate.getMonth() && today.getDate() < birthDate.getDate())) age--;
  return age;
}

function setAuthMode(nextMode) {
  authMode = nextMode;
  const registering = authMode === "register";
  const fields = ["name-field", "gender-field", "birth-field", "terms-field"];
  document.querySelector("#login-tab").classList.toggle("is-active", !registering);
  document.querySelector("#register-tab").classList.toggle("is-active", registering);
  document.querySelector("#login-tab").setAttribute("aria-selected", String(!registering));
  document.querySelector("#register-tab").setAttribute("aria-selected", String(registering));
  for (const id of fields) {
    const field = document.getElementById(id);
    field.hidden = !registering;
    const input = field.querySelector("input, select");
    input.disabled = !registering;
    input.required = registering;
  }
  authForm.elements.password.autocomplete = registering ? "new-password" : "current-password";
  document.querySelector("#auth-title").textContent = registering ? "Tạo tài khoản." : "Chào bạn.";
  document.querySelector("#auth-kicker").textContent = registering ? "BẮT ĐẦU MỘT CÂU CHUYỆN MỚI" : "RẤT VUI ĐƯỢC GẶP LẠI";
  document.querySelector("#auth-subtitle").textContent = registering
    ? "Tạo hồ sơ Kiss và mở đầu một cuộc gặp gỡ."
    : "Đăng nhập để tiếp tục hành trình gặp gỡ.";
  authButton.innerHTML = `${registering ? "Tạo tài khoản" : "Đăng nhập"} <span aria-hidden="true">↗</span>`;
  authMessage.hidden = true;
  authMessage.classList.remove("is-success");
}

async function request(path, options = {}) {
  const headers = new Headers(options.headers || {});
  if (appState.token) headers.set("Authorization", `Bearer ${appState.token}`);
  if (options.body && !(options.body instanceof FormData)) headers.set("Content-Type", "application/json");
  const response = await fetch(`/api${path}`, { ...options, headers });
  if (response.status === 204) return null;
  const contentType = response.headers.get("content-type") || "";
  const result = contentType.includes("json") ? await response.json() : await response.text();
  if (!response.ok) throw new Error(result?.detail || result?.title || "Không thể hoàn tất yêu cầu.");
  return result;
}

async function submitAuth(event) {
  event.preventDefault();
  if (!authForm.reportValidity()) return;
  const values = new FormData(authForm);
  const registering = authMode === "register";
  const payload = { email: values.get("email").trim(), password: values.get("password") };
  if (registering) {
    payload.name = values.get("name").trim();
    payload.birthDate = values.get("birthDate");
    payload.gender = values.get("gender");
    payload.acceptTerms = values.get("acceptTerms") === "on";
  }

  authButton.disabled = true;
  authButton.textContent = "Đang xử lý...";
  authMessage.hidden = true;
  try {
    const session = await request(`/auth/${registering ? "register" : "login"}`, {
      method: "POST",
      body: JSON.stringify(payload)
    });
    appState.token = session.accessToken;
    localStorage.setItem("kiss.accessToken", session.accessToken);
    appState.profile = await request("/profiles/me");
    await enterWorkspace();
  } catch (error) {
    authMessage.textContent = error.message;
    authMessage.hidden = false;
  } finally {
    authButton.disabled = false;
    authButton.innerHTML = `${registering ? "Tạo tài khoản" : "Đăng nhập"} <span aria-hidden="true">↗</span>`;
  }
}

async function enterWorkspace() {
  authView.hidden = true;
  appView.hidden = false;
  document.title = "Kiss | Khám phá";
  document.querySelector("#app-user-name").textContent = appState.profile.name;
  await loadInterests();
  fillProfile();
  await Promise.all([loadDiscover(), loadMatches(), loadJourney()]);
  showPage("discover");
}

function showPage(page) {
  for (const id of ["discover", "journey", "profile", "matches"]) {
    document.getElementById(`${id}-page`).hidden = id !== page;
  }
  document.querySelectorAll("[data-page]").forEach((button) => {
    button.classList.toggle("is-active", button.dataset.page === page);
  });
  if (page === "discover") loadDiscover();
  if (page === "profile") loadProfile();
  if (page === "journey") loadJourney();
  if (page === "matches") loadMatches();
}

function notify(element, text, success = false) {
  element.textContent = text;
  element.classList.toggle("is-success", success);
  element.hidden = false;
}

async function loadInterests() {
  appState.interests = await request("/interests");
  const filter = document.querySelector("#filter-interest");
  filter.innerHTML = '<option value="">Tất cả</option>' + appState.interests.map((interest) =>
    `<option value="${interest.id}">${escapeHtml(interest.name)}</option>`).join("");
  document.querySelector("#interest-options").innerHTML = appState.interests.map((interest) => `
    <label class="interest-option"><input type="checkbox" name="interestIds" value="${interest.id}"><span>${escapeHtml(interest.name)}</span></label>`).join("");
}

async function loadDiscover() {
  const form = document.querySelector("#discover-filters");
  const values = new FormData(form);
  const params = new URLSearchParams({ limit: "30" });
  for (const key of ["search", "gender", "minAge", "maxAge", "city", "lookingFor", "interestId"]) {
    const value = values.get(key);
    if (value) params.set(key, value);
  }
  const results = document.querySelector("#discover-results");
  const message = document.querySelector("#discover-message");
  message.hidden = true;
  results.innerHTML = '<p class="empty-message">Đang tìm hồ sơ phù hợp...</p>';
  try {
    const profiles = await request(`/profiles/discover?${params.toString()}`);
    renderProfiles(results, profiles, true);
  } catch (error) {
    results.replaceChildren();
    notify(message, error.message);
  }
}

function renderProfiles(container, profiles, allowSwipe) {
  if (!profiles.length) {
    container.innerHTML = '<p class="empty-message">Chưa tìm thấy hồ sơ phù hợp. Hãy thử điều chỉnh bộ lọc hoặc quay lại sau.</p>';
    return;
  }
  container.innerHTML = profiles.map((profile) => {
    const avatar = profile.photos?.find((photo) => photo.isAvatar) || profile.photos?.[0];
    const image = photoUrl(avatar?.url);
    return `<article class="person-card" data-profile-id="${profile.id}">
      ${image ? `<img class="person-photo" src="${escapeHtml(image)}" alt="Ảnh hồ sơ ${escapeHtml(profile.name)}">` : `<div class="person-photo person-photo-placeholder" aria-hidden="true">${escapeHtml(profile.name.trim().charAt(0).toUpperCase())}</div>`}
      <div class="person-card-body"><div class="person-card-heading"><h2>${escapeHtml(profile.name)}</h2><span>${ageFrom(profile.birthDate)}</span></div>
      <div class="person-meta">${profile.city ? `<span>${escapeHtml(profile.city)}</span>` : ""}<span>${escapeHtml(profile.lookingFor || "Dating")}</span></div>
      <p class="person-bio">${escapeHtml(profile.bio || "Chưa có phần giới thiệu.")}</p>
      <div class="person-interests">${(profile.interests || []).map((interest) => `<span class="interest-chip">${escapeHtml(interest.name)}</span>`).join("")}</div>
      ${allowSwipe ? `<div class="person-card-actions"><button type="button" data-swipe="false">Bỏ qua</button><button type="button" data-swipe="true">Thích ♡</button></div>` : ""}</div></article>`;
  }).join("");
}

async function loadProfile() {
  try {
    appState.profile = await request("/profiles/me");
    fillProfile();
    renderPhotos();
  } catch (error) {
    notify(document.querySelector("#profile-message"), error.message);
  }
}

function fillProfile() {
  const form = document.querySelector("#profile-form");
  if (!appState.profile) return;
  for (const key of ["name", "birthDate", "gender", "city", "occupation", "education", "lookingFor", "bio"]) {
    form.elements[key].value = appState.profile[key] || "";
  }
  const interestIds = new Set((appState.profile.interests || []).map((interest) => interest.id));
  document.querySelectorAll("input[name=interestIds]").forEach((input) => {
    input.checked = interestIds.has(input.value);
  });
}

function renderPhotos() {
  const photos = appState.profile?.photos || [];
  document.querySelector("#photo-list").innerHTML = photos.length
    ? photos.map((photo) => `<div class="photo-row" data-photo-id="${photo.id}"><img src="${escapeHtml(photoUrl(photo.url))}" alt="Ảnh hồ sơ"><span>${photo.isAvatar ? "Ảnh đại diện" : "Ảnh hồ sơ"}</span>${photo.isAvatar ? "" : `<button type="button" data-avatar>Đặt đại diện</button>`}<button type="button" data-delete-photo>Xóa</button></div>`).join("")
    : '<p class="empty-message">Bạn chưa thêm ảnh hồ sơ.</p>';
}

async function saveProfile(event) {
  event.preventDefault();
  const form = new FormData(event.currentTarget);
  const message = document.querySelector("#profile-message");
  message.hidden = true;
  try {
    appState.profile = await request("/profiles/me", {
      method: "PUT",
      body: JSON.stringify(Object.fromEntries(["name", "birthDate", "gender", "city", "occupation", "education", "lookingFor", "bio"].map((key) => [key, form.get(key)])))
    });
    const interestIds = [...document.querySelectorAll("input[name=interestIds]:checked")].map((input) => input.value);
    await request("/profiles/me/interests", { method: "PUT", body: JSON.stringify({ interestIds }) });
    appState.profile = await request("/profiles/me");
    fillProfile();
    renderPhotos();
    notify(message, "Đã lưu hồ sơ và sở thích.", true);
  } catch (error) {
    notify(message, error.message);
  }
}

async function uploadPhoto(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const message = document.querySelector("#profile-message");
  try {
    const photo = await request("/profiles/me/photos", { method: "POST", body: new FormData(form) });
    form.reset();
    appState.profile.photos = [...(appState.profile.photos || []), photo];
    renderPhotos();
    notify(message, "Đã tải ảnh lên.", true);
  } catch (error) {
    notify(message, error.message);
  }
}

async function managePhoto(event) {
  const button = event.target.closest("[data-avatar], [data-delete-photo]");
  if (!button) return;
  const row = button.closest("[data-photo-id]");
  const photoId = row.dataset.photoId;
  const message = document.querySelector("#profile-message");
  try {
    if (button.hasAttribute("data-avatar")) {
      await request(`/profiles/me/photos/${photoId}/avatar`, { method: "PUT" });
    } else {
      await request(`/profiles/me/photos/${photoId}`, { method: "DELETE" });
    }
    await loadProfile();
    notify(message, button.hasAttribute("data-avatar") ? "Đã đổi ảnh đại diện." : "Đã xóa ảnh.", true);
  } catch (error) {
    notify(message, error.message);
  }
}

async function swipe(event) {
  const button = event.target.closest("[data-swipe]");
  if (!button) return;
  const card = button.closest("[data-profile-id]");
  const buttons = card.querySelectorAll("button");
  buttons.forEach((item) => { item.disabled = true; });
  try {
    const result = await request("/swipes", {
      method: "POST",
      body: JSON.stringify({ targetUserId: card.dataset.profileId, isLike: button.dataset.swipe === "true" })
    });
    card.remove();
    if (result.isMatch) {
      await loadMatches();
      notify(document.querySelector("#discover-message"), "Hai bạn đã thích nhau! Đã có một kết đôi mới.", true);
    }
    if (!document.querySelector("#discover-results .person-card")) {
      document.querySelector("#discover-results").innerHTML = '<p class="empty-message">Đã xem hết các hồ sơ trong kết quả lọc.</p>';
    }
  } catch (error) {
    buttons.forEach((item) => { item.disabled = false; });
    notify(document.querySelector("#discover-message"), error.message);
  }
}

async function loadMatches() {
  try {
    const matches = await request("/matches");
    document.querySelector("#match-count").textContent = matches.length;
    renderProfiles(document.querySelector("#matches-results"), matches.map((match) => match.profile), false);
  } catch (error) {
    notify(document.querySelector("#discover-message"), error.message);
  }
}

function updateJourneyPreview() {
  const input = document.querySelector("#journey-media");
  const preview = document.querySelector("#journey-media-preview");
  const fileName = document.querySelector("#journey-file-name");
  const file = input.files?.[0];
  if (!file) {
    preview.hidden = true;
    preview.innerHTML = "";
    fileName.textContent = "PNG, JPEG, WebP hoặc MP4, WebM";
    return;
  }

  fileName.textContent = file.name;
  const url = URL.createObjectURL(file);
  const mediaMarkup = file.type.startsWith("video/")
    ? `<video src="${url}" controls playsinline></video>`
    : `<img src="${url}" alt="Xem trước media hành trình" />`;

  preview.hidden = false;
  preview.innerHTML = mediaMarkup;
}

function clearJourneyPreview() {
  const input = document.querySelector("#journey-media");
  const preview = document.querySelector("#journey-media-preview");
  const fileName = document.querySelector("#journey-file-name");
  if (input) input.value = "";
  preview.hidden = true;
  preview.innerHTML = "";
  fileName.textContent = "PNG, JPEG, WebP hoặc MP4, WebM";
}

async function submitJourneyPost(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const message = document.querySelector("#journey-message");
  const fileInput = document.querySelector("#journey-media");
  const formData = new FormData(form);
  if (fileInput.files?.[0]) {
    formData.set("media", fileInput.files[0]);
  }
  message.hidden = true;

  try {
    const result = await request("/journey", {
      method: "POST",
      body: formData
    });
    form.reset();
    clearJourneyPreview();
    await loadJourney();
    notify(message, result ? "Bài viết của bạn đã xuất hiện trên hành trình." : "Đã đăng lên hành trình.", true);
  } catch (error) {
    notify(message, error.message);
  }
}

async function loadJourney() {
  const feed = document.querySelector("#journey-feed");
  try {
    const posts = await request("/journey?limit=20");
    if (!posts.length) {
      feed.innerHTML = '<p class="empty-message">Chưa có bài viết nào trong hành trình. Hãy chia sẻ điều đầu tiên của bạn.</p>';
      return;
    }

    feed.innerHTML = posts.map((post) => {
      const media = post.mediaUrl
        ? post.mediaType?.startsWith("video/")
          ? `<video class="journey-media" src="${escapeHtml(post.mediaUrl)}" controls playsinline></video>`
          : `<img class="journey-media" src="${escapeHtml(post.mediaUrl)}" alt="Ảnh hành trình của ${escapeHtml(post.authorName)}">`
        : "";
      const deleteButton = post.isMine
        ? `<button class="journey-delete" data-journey-delete="${post.id}" type="button">Xóa</button>`
        : "";
      const likeLabel = post.isLikedByMe ? "Đã thích" : "Thích";
      const initials = (post.authorName || "K").trim().charAt(0).toUpperCase();
      return `
        <article class="journey-card" data-journey-post-id="${post.id}">
          <header class="journey-header">
            <div class="journey-author-avatar">${escapeHtml(initials)}</div>
            <div class="journey-author-meta">
              <strong>${escapeHtml(post.authorName)}</strong>
              <time>${new Date(post.createdAt).toLocaleString("vi-VN", { dateStyle: "medium", timeStyle: "short" })}</time>
            </div>
            ${deleteButton}
          </header>
          ${post.content ? `<p class="journey-content">${escapeHtml(post.content)}</p>` : ""}
          ${media}
          <footer class="journey-actions">
            <button class="journey-action-button ${post.isLikedByMe ? "is-liked" : ""}" data-journey-like="${post.id}" type="button">${likeLabel} (${post.likesCount ?? 0})</button>
            <button class="journey-action-button" data-journey-comment-toggle="${post.id}" type="button">Bình luận (${post.commentsCount ?? 0})</button>
          </footer>
          <div class="journey-comment-box" data-journey-comments="${post.id}" hidden>
            <div class="journey-comments" data-journey-comment-list="${post.id}"></div>
            <form class="journey-comment-form" data-journey-comment-form="${post.id}">
              <input name="content" maxlength="500" placeholder="Viết bình luận..." required>
              <button type="submit">Gửi</button>
            </form>
          </div>
        </article>`;
    }).join("");
    document.querySelectorAll("[data-journey-comment-toggle]").forEach((button) => button.addEventListener("click", async () => {
      const postId = button.dataset.journeyCommentToggle;
      const box = document.querySelector(`[data-journey-comments="${postId}"]`);
      if (!box) return;
      const nextState = box.hidden;
      box.hidden = !nextState;
      if (nextState) await loadJourneyComments(postId);
    }));
    document.querySelectorAll("[data-journey-like]").forEach((button) => button.addEventListener("click", () => toggleJourneyLike(button.dataset.journeyLike)));
    document.querySelectorAll("[data-journey-comment-form]").forEach((form) => form.addEventListener("submit", submitJourneyComment));
  } catch (error) {
    feed.innerHTML = `<p class="empty-message">${escapeHtml(error.message)}</p>`;
  }
}

async function loadJourneyComments(postId) {
  const list = document.querySelector(`[data-journey-comment-list="${postId}"]`);
  if (!list) return;
  try {
    const comments = await request(`/journey/${postId}/comments`);
    list.innerHTML = comments.length
      ? comments.map((comment) => `
          <div class="journey-comment-item">
            <strong>${escapeHtml(comment.authorName)}</strong>
            <span>${escapeHtml(comment.content)}</span>
          </div>`).join("")
      : '<p class="journey-empty-comment">Chưa có bình luận nào.</p>';
  } catch (error) {
    list.innerHTML = `<p class="journey-empty-comment">${escapeHtml(error.message)}</p>`;
  }
}

async function toggleJourneyLike(postId) {
  try {
    const result = await request(`/journey/${postId}/like`, { method: "POST" });
    await loadJourney();
    notify(document.querySelector("#journey-message"), result.liked ? "Bạn đã thích bài này." : "Bạn đã bỏ thích bài này.", true);
  } catch (error) {
    notify(document.querySelector("#journey-message"), error.message);
  }
}

async function submitJourneyComment(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const postId = form.dataset.journeyCommentForm;
  const content = new FormData(form).get("content");
  if (!content || !String(content).trim()) return;
  try {
    await request(`/journey/${postId}/comments`, {
      method: "POST",
      body: JSON.stringify({ content: String(content).trim() })
    });
    form.reset();
    await loadJourney();
    const box = document.querySelector(`[data-journey-comments="${postId}"]`);
    if (box) box.hidden = false;
    notify(document.querySelector("#journey-message"), "Bình luận đã được gửi.", true);
  } catch (error) {
    notify(document.querySelector("#journey-message"), error.message);
  }
}

async function deleteJourneyPost(event) {
  const button = event.target.closest("[data-journey-delete]");
  if (!button) return;
  const message = document.querySelector("#journey-message");
  try {
    await request(`/journey/${button.dataset.journeyDelete}`, { method: "DELETE" });
    await loadJourney();
    notify(message, "Đã xóa bài viết của bạn.", true);
  } catch (error) {
    notify(message, error.message);
  }
}

function logout() {
  localStorage.removeItem("kiss.accessToken");
  localStorage.removeItem("kiss.profile");
  appState.token = null;
  appState.profile = null;
  appView.hidden = true;
  authView.hidden = false;
  document.title = "Kiss | Đăng nhập";
  authForm.reset();
  setAuthMode("login");
}

async function startApp() {
  document.querySelector("#login-tab").addEventListener("click", () => setAuthMode("login"));
  document.querySelector("#register-tab").addEventListener("click", () => setAuthMode("register"));
  authForm.addEventListener("submit", submitAuth);
  document.querySelector("#discover-filters").addEventListener("submit", (event) => { event.preventDefault(); loadDiscover(); });
  document.querySelector("#profile-form").addEventListener("submit", saveProfile);
  document.querySelector("#photo-form").addEventListener("submit", uploadPhoto);
  document.querySelector("#photo-list").addEventListener("click", managePhoto);
  document.querySelector("#discover-results").addEventListener("click", swipe);
  document.querySelector("#journey-form").addEventListener("submit", submitJourneyPost);
  document.querySelector("#journey-media").addEventListener("change", updateJourneyPreview);
  document.querySelector("#journey-refresh").addEventListener("click", loadJourney);
  document.querySelector("#journey-feed").addEventListener("click", deleteJourneyPost);
  document.querySelectorAll("[data-page]").forEach((button) => button.addEventListener("click", () => showPage(button.dataset.page)));
  document.querySelector("#logout-button").addEventListener("click", logout);

  if (!appState.token) return;
  try {
    appState.profile = await request("/profiles/me");
    await enterWorkspace();
  } catch {
    logout();
  }
}

startApp();
