const authView = document.querySelector(".auth-layout");
const authForm = document.querySelector("#auth-form");
const authTabs = document.querySelector(".auth-tabs");
const forgotPasswordForm = document.querySelector("#forgot-password-form");
const resetPasswordForm = document.querySelector("#reset-password-form");
const appView = document.querySelector("#dating-app");
const authMessage = document.querySelector("#auth-message");
const authButton = document.querySelector("#auth-submit");
const appState = { token: localStorage.getItem("kiss.accessToken"), profile: null, interests: [], resetToken: null, connections: [], conversations: [], selectedConversationId: null, chatPoll: null, presenceTimer: null, notifications: [], notificationIds: new Set(), notificationsInitialized: false, pendingDateRecipientId: null, discoverProfiles: [], discoverPage: 1, discoverPageSize: 30, adminUsers: [], adminReports: [], canModerateReports: false, isOwner: false };
const chatState = {
  contacts: [],
  activeUserId: null,
  pendingUserId: null,
  pollTimer: null,
  isLoading: false,
  messages: [],
  lastMessageKey: null,
  mediaUrls: new Map()
};
let authMode = "login";

const escapeHtml = (value = "") => String(value).replace(/[&<>"']/g, (character) => ({
  "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
}[character]));

function photoUrl(value) {
  return typeof value === "string" && (value.startsWith("/uploads/") || value.startsWith("/demo-profiles/")) ? value : "";
}

function cleanProfileName(value = "") {
  return String(value).replace(/^\[M\u1eabu\]\s*/u, "");
}

function ageFrom(value) {
  if (!value) return "Đã ẩn tuổi";
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
  authTabs.hidden = false;
  authForm.hidden = false;
  forgotPasswordForm.hidden = true;
  resetPasswordForm.hidden = true;
  document.querySelector("#forgot-password-link").hidden = registering;
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
  document.querySelector("#privacy-note").hidden = !registering;
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
  if (!response.ok) {
    const validationError = Object.values(result?.errors || {}).flat()[0];
    throw new Error(result?.detail || validationError || result?.title || "Không thể hoàn tất yêu cầu.");
  }
  return result;
}

function showForgotPassword() {
  authTabs.hidden = true;
  authForm.hidden = true;
  document.querySelector("#forgot-password-link").hidden = true;
  forgotPasswordForm.hidden = false;
  resetPasswordForm.hidden = true;
  document.querySelector("#auth-title").textContent = "Quên mật khẩu?";
  document.querySelector("#auth-kicker").textContent = "KHÔI PHỤC TÀI KHOẢN";
  document.querySelector("#auth-subtitle").textContent = "Nhập email đã đăng ký để nhận liên kết đặt lại mật khẩu.";
  document.querySelector("#forgot-password-message").hidden = true;
}

function showResetPassword(token) {
  appState.resetToken = token;
  authTabs.hidden = true;
  authForm.hidden = true;
  forgotPasswordForm.hidden = true;
  resetPasswordForm.hidden = false;
  document.querySelector("#forgot-password-link").hidden = true;
  document.querySelector("#auth-title").textContent = "Đặt mật khẩu mới.";
  document.querySelector("#auth-kicker").textContent = "KHÔI PHỤC TÀI KHOẢN";
  document.querySelector("#auth-subtitle").textContent = "Chọn mật khẩu mới có ít nhất 8 ký tự.";
  document.querySelector("#reset-password-message").hidden = true;
}

async function submitForgotPassword(event) {
  event.preventDefault();
  if (!forgotPasswordForm.reportValidity()) return;
  const button = forgotPasswordForm.querySelector("button[type=submit]");
  const message = document.querySelector("#forgot-password-message");
  button.disabled = true;
  message.hidden = true;
  try {
    const email = new FormData(forgotPasswordForm).get("email").trim();
    const result = await request("/auth/forgot-password", {
      method: "POST",
      body: JSON.stringify({ email })
    });
    notify(message, result.message, true);
  } catch (error) {
    notify(message, error.message);
  } finally {
    button.disabled = false;
  }
}

async function submitResetPassword(event) {
  event.preventDefault();
  if (!resetPasswordForm.reportValidity()) return;
  const values = new FormData(resetPasswordForm);
  const newPassword = values.get("newPassword");
  const message = document.querySelector("#reset-password-message");
  if (newPassword !== values.get("confirmPassword")) {
    notify(message, "Hai mật khẩu chưa khớp.");
    return;
  }

  const button = resetPasswordForm.querySelector("button[type=submit]");
  button.disabled = true;
  message.hidden = true;
  try {
    await request("/auth/reset-password", {
      method: "POST",
      body: JSON.stringify({ token: appState.resetToken, newPassword })
    });
    appState.resetToken = null;
    appState.token = null;
    localStorage.removeItem("kiss.accessToken");
    localStorage.removeItem("kiss.profile");
    resetPasswordForm.reset();
    setAuthMode("login");
    notify(authMessage, "Mật khẩu đã được đổi. Hãy đăng nhập bằng mật khẩu mới.", true);
  } catch (error) {
    notify(message, error.message);
  } finally {
    button.disabled = false;
  }
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
  startPresenceHeartbeat();
  await loadInterests();
  fillProfile();
  await Promise.all([loadDiscover(), loadLikedProfiles(), loadRejectedProfiles(), loadJourney(), loadAdminOverview(), loadNotifications()]);
  showPage("discover");
  const sharedPostId = new URLSearchParams(window.location.search).get("post");
  if (sharedPostId) {
    showPage("journey");
    await loadJourney();
    document.querySelector(`[data-journey-post-id="${CSS.escape(sharedPostId)}"]`)?.scrollIntoView({ behavior: "smooth", block: "center" });
  }
}

function showPage(page) {
  if (page === "events" && !appState.isOwner) page = "notifications";
  for (const id of ["discover", "journey", "profile", "matches", "passed", "notifications", "dates", "events", "chat", "connections", "messages", "admin"]) {
    document.getElementById(`${id}-page`).hidden = id !== page;
  }
  window.scrollTo(0, 0);
  document.querySelectorAll("[data-page]").forEach((button) => {
    button.classList.toggle("is-active", button.dataset.page === page);
  });
  if (page === "discover") loadDiscover();
  if (page === "profile") loadProfile();
  if (page === "journey") loadJourney();
  if (page === "matches") loadLikedProfiles();
  if (page === "passed") loadRejectedProfiles();
  if (page === "connections") loadConnections();
  if (page === "chat") loadChatContacts();
  else stopChatPolling();
  if (page === "messages") startSocialPolling();
  else stopSocialPolling();
  if (page === "admin") loadAdminOverview();
  if (page === "notifications") loadNotifications();
  if (page === "dates") loadDates();
  if (page === "events") loadCommunityEvents();
}

function startPresenceHeartbeat() {
  stopPresenceHeartbeat();
  const heartbeat = () => {
    request("/profiles/me/presence", { method: "POST" }).catch(() => {});
    loadNotifications().catch(() => {});
  };
  heartbeat();
  appState.presenceTimer = window.setInterval(heartbeat, 60000);
}

function stopPresenceHeartbeat() {
  if (appState.presenceTimer) window.clearInterval(appState.presenceTimer);
  appState.presenceTimer = null;
}

async function loadAdminOverview() {
  const nav = document.querySelector("#admin-nav");
  try {
    const access = await request("/admin/access");
    nav.hidden = false;
    const isOwner = Boolean(access.isOwner);
    appState.isOwner = isOwner;
    document.querySelector("#events-nav").hidden = !isOwner;
    appState.canModerateReports = Boolean(access.canModerate || isOwner);
    document.querySelector("#admin-stat-grid").hidden = !isOwner;
    document.querySelector("#admin-user-form").closest(".admin-users-panel").hidden = !isOwner;
    document.querySelector("#admin-users").closest(".admin-users-panel").hidden = !isOwner;
    document.querySelector("#admin-announcement-panel").hidden = !isOwner;
    document.querySelector("#admin-analytics-panel").hidden = !isOwner;
    document.querySelector("#admin-audit-panel").hidden = !isOwner;
    const reports = await request("/admin/reports");
    appState.adminReports = reports;
    renderAdminReports(reports);
    if (!isOwner) return;
    const [overview, users, audit, analytics] = await Promise.all([
      request("/admin/overview"), request("/admin/users"), request("/admin/audit"), request("/admin/analytics")
    ]);
    appState.adminUsers = users;
    document.querySelector("#admin-stat-grid").innerHTML = [
      ["Tài khoản", overview.userCount], ["Lượt thích/bỏ qua", overview.swipeCount],
      ["Lượt kết đôi", overview.matchCount], ["Bài viết", overview.journeyPostCount],
      ["Bình luận", overview.journeyCommentCount], ["Tin nhắn (chỉ số lượng)", overview.chatMessageCount]
    ].map(([label, value]) => `<article class="admin-stat"><span>${escapeHtml(label)}</span><strong>${value}</strong></article>`).join("");
    renderAdminUsers(users);
    renderAdminAudit(audit);
    renderAdminAnalytics(analytics);
    appState.adminReports = reports;
    document.querySelector("#admin-message").hidden = true;
  } catch {
    if (!appState.isOwner) {
      document.querySelector("#events-nav").hidden = true;
      nav.hidden = true;
    }
  }
}

function renderAdminAudit(rows) {
  const container = document.querySelector("#admin-audit-list");
  container.innerHTML = rows.length ? rows.map((row) => `
    <article class="admin-audit-row"><div><strong>${escapeHtml(row.action)} · ${escapeHtml(row.targetType)}</strong><span>${escapeHtml(row.adminName)} · ${new Date(row.createdAt).toLocaleString("vi-VN")}</span></div><p>${escapeHtml(row.details)}</p></article>`).join("")
    : '<p class="empty-message">Chưa có hoạt động quản trị.</p>';
}

function renderAdminAnalytics(rows) {
  const container = document.querySelector("#admin-analytics");
  const maximum = Math.max(1, ...rows.flatMap((row) => [row.newUsers, row.swipes, row.matches]));
  container.innerHTML = rows.map((row) => `
    <div class="analytics-day" title="${new Date(row.date).toLocaleDateString("vi-VN")}: ${row.newUsers} tài khoản, ${row.swipes} lượt tương tác, ${row.matches} kết đôi">
      <div class="analytics-bars"><i class="analytics-user" style="height:${Math.max(2, row.newUsers / maximum * 100)}%"></i><i class="analytics-swipe" style="height:${Math.max(2, row.swipes / maximum * 100)}%"></i><i class="analytics-match" style="height:${Math.max(2, row.matches / maximum * 100)}%"></i></div>
      <time>${new Date(row.date).toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit" })}</time>
    </div>`).join("");
}

async function sendSystemAnnouncement(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const values = new FormData(form);
  const button = form.querySelector("[type=submit]");
  button.disabled = true;
  try {
    const result = await request("/admin/notifications", {
      method: "POST",
      body: JSON.stringify({ title: values.get("title").trim(), body: values.get("body").trim() })
    });
    form.reset();
    notify(document.querySelector("#admin-announcement-message"), `Đã gửi thông báo đến ${result.recipientCount} tài khoản.`, true);
    await loadNotifications();
    await loadAdminOverview();
  } catch (error) {
    notify(document.querySelector("#admin-announcement-message"), error.message);
  } finally {
    button.disabled = false;
  }
}

function renderAdminReports(reports) {
  const container = document.querySelector("#admin-reports");
  const pendingCount = reports.filter((report) => report.status === "Pending").length;
  document.querySelector("#admin-reports-heading").textContent = `Báo cáo cần xử lý (${pendingCount})`;
  if (!reports.length) {
    container.innerHTML = '<p class="empty-message">Chưa có báo cáo nào.</p>';
    return;
  }
  const targetTypeLabels = { User: "Tài khoản", Post: "Bài viết", Comment: "Bình luận", Message: "Tin nhắn" };
  container.innerHTML = reports.map((report) => `
    <article class="admin-report-card ${report.status === "Pending" ? "is-pending" : ""}">
      <div class="admin-report-copy">
        <div><strong>${escapeHtml(targetTypeLabels[report.targetType] || report.targetType)} · ${escapeHtml(report.status)}</strong><small>Người tố cáo: ${escapeHtml(report.reporterName)} · ${new Date(report.createdAt).toLocaleString("vi-VN")}</small></div>
        ${report.targetName ? `<p><b>Người bị báo cáo:</b> ${escapeHtml(report.targetName)}${report.targetIsSuspended ? " · Tài khoản đã bị khóa" : ""}</p>` : ""}
        <p>${escapeHtml(report.reason)}</p>
        <blockquote>${escapeHtml(report.targetPreview || "Không còn nội dung để xem")}</blockquote>
        <small>Mã nội dung: ${escapeHtml(report.targetId)}</small>
      </div>
      ${report.status === "Pending" && appState.canModerateReports ? `<div class="admin-user-actions"><button type="button" data-report-resolve="${escapeHtml(report.id)}">${report.targetType === "User" ? "Khóa tài khoản" : "Ẩn nội dung"}</button><button type="button" data-report-dismiss="${escapeHtml(report.id)}">Bỏ qua</button></div>` : ""}
    </article>`).join("");
}

async function updateAdminReport(reportId, resolve) {
  const report = appState.adminReports.find((item) => item.id === reportId);
  if (!report) return;
  const hideTarget = resolve;
  const action = report.targetType === "User" ? "khóa tài khoản này" : "ẩn nội dung này";
  if (hideTarget && !window.confirm(`Xác nhận ${action}?`)) return;
  try {
    await request(`/admin/reports/${reportId}`, {
      method: "PUT",
      body: JSON.stringify({ status: resolve ? "Resolved" : "Dismissed", hideTarget })
    });
    await loadAdminOverview();
  } catch (error) {
    notify(document.querySelector("#admin-message"), error.message);
  }
}

function renderAdminUsers(users) {
  const container = document.querySelector("#admin-users");
  container.innerHTML = users.length
    ? `<div class="admin-user-table-wrap"><table class="admin-user-table"><thead><tr><th>Họ tên</th><th>Email</th><th>Vai trò</th><th>Thành phố</th><th>Ngày tạo</th><th>Thao tác</th></tr></thead><tbody>${users.map((user) => `
      <tr>
        <td><strong>${escapeHtml(user.name)}</strong></td>
        <td>${escapeHtml(user.email)}</td>
        <td>${escapeHtml(user.role || "User")}</td>
        <td>${escapeHtml(user.city || "—")}</td>
        <td><time>${new Date(user.createdAt).toLocaleDateString("vi-VN")}</time></td>
        <td><div class="admin-user-actions"><button class="quiet-button" type="button" data-admin-edit="${escapeHtml(user.id)}">Sửa</button><button class="admin-delete-button" type="button" data-admin-delete="${escapeHtml(user.id)}" ${user.id === appState.profile?.id ? "disabled title=\"Không thể xóa tài khoản đang đăng nhập\"" : ""}>Xóa</button></div></td>
      </tr>`).join("")}</tbody></table></div>`
    : '<p class="empty-message">Chưa có tài khoản.</p>';
}

function resetAdminUserForm() {
  const form = document.querySelector("#admin-user-form");
  const password = document.querySelector("#admin-user-password");
  form.reset();
  delete form.dataset.userId;
  password.required = true;
  password.placeholder = "";
  document.querySelector("#admin-password-hint").textContent = "Tối thiểu 8 ký tự";
  document.querySelector("#admin-user-form-title").textContent = "Thêm tài khoản";
  document.querySelector("#admin-user-submit").textContent = "Thêm tài khoản";
  document.querySelector("#admin-user-cancel").hidden = true;
}

function editAdminUser(userId) {
  const user = appState.adminUsers.find((candidate) => candidate.id === userId);
  if (!user) return;

  const form = document.querySelector("#admin-user-form");
  form.dataset.userId = user.id;
  form.elements.name.value = user.name;
  form.elements.email.value = user.email;
  form.elements.birthDate.value = user.birthDate;
  form.elements.gender.value = user.gender;
  form.elements.city.value = user.city || "";
  form.elements.occupation.value = user.occupation || "";
  form.elements.education.value = user.education || "";
  form.elements.lookingFor.value = user.lookingFor;
  form.elements.role.value = user.role || "User";
  form.elements.bio.value = user.bio || "";
  const password = document.querySelector("#admin-user-password");
  password.value = "";
  password.required = false;
  password.placeholder = "Để trống để giữ mật khẩu hiện tại";
  document.querySelector("#admin-password-hint").textContent = "Không bắt buộc khi sửa";
  document.querySelector("#admin-user-form-title").textContent = `Sửa tài khoản: ${user.name}`;
  document.querySelector("#admin-user-submit").textContent = "Lưu thay đổi";
  document.querySelector("#admin-user-cancel").hidden = false;
  form.scrollIntoView({ behavior: "smooth", block: "start" });
}

async function submitAdminUser(event) {
  event.preventDefault();
  const form = event.currentTarget;
  if (!form.reportValidity()) return;

  const values = new FormData(form);
  const password = values.get("password");
  const body = {
    name: values.get("name").trim(),
    email: values.get("email").trim(),
    birthDate: values.get("birthDate"),
    gender: values.get("gender"),
    city: values.get("city").trim() || null,
    occupation: values.get("occupation").trim() || null,
    education: values.get("education").trim() || null,
    lookingFor: values.get("lookingFor"),
    role: values.get("role"),
    bio: values.get("bio").trim()
  };
  const userId = form.dataset.userId;
  const button = document.querySelector("#admin-user-submit");
  const message = document.querySelector("#admin-message");
  button.disabled = true;
  message.hidden = true;
  try {
    if (userId) {
      body.newPassword = password || null;
      const updatedUser = await request(`/admin/users/${userId}`, { method: "PUT", body: JSON.stringify(body) });
      if (appState.profile?.id === userId) {
        appState.profile = { ...appState.profile, ...updatedUser };
        document.querySelector("#app-user-name").textContent = updatedUser.name;
      }
    } else {
      body.password = password;
      await request("/admin/users", { method: "POST", body: JSON.stringify(body) });
    }
    resetAdminUserForm();
    await loadAdminOverview();
    notify(message, userId ? "Đã cập nhật tài khoản." : "Đã thêm tài khoản mới.", true);
  } catch (error) {
    notify(message, error.message);
  } finally {
    button.disabled = false;
  }
}

async function deleteAdminUser(userId) {
  const user = appState.adminUsers.find((candidate) => candidate.id === userId);
  if (!user || !window.confirm(`Xóa tài khoản ${user.name}? Hồ sơ, kết nối và tin nhắn liên quan cũng sẽ bị xóa.`)) return;

  const message = document.querySelector("#admin-message");
  message.hidden = true;
  try {
    await request(`/admin/users/${userId}`, { method: "DELETE" });
    if (document.querySelector("#admin-user-form").dataset.userId === userId) resetAdminUserForm();
    await loadAdminOverview();
    notify(message, `Đã xóa tài khoản ${user.name}.`, true);
  } catch (error) {
    notify(message, error.message);
  }
}

async function exportAdminData() {
  const button = document.querySelector("#admin-export");
  const message = document.querySelector("#admin-message");
  button.disabled = true;
  try {
    const data = await request("/admin/export");
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: "application/json;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `kiss-data-${new Date().toISOString().slice(0, 10)}.json`;
    document.body.append(anchor);
    anchor.click();
    anchor.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    notify(message, "Đã tải bản xuất JSON.", true);
  } catch (error) {
    notify(message, error.message);
  } finally {
    button.disabled = false;
  }
}

function socialAvatar(user, className = "social-avatar") {
  const initial = escapeHtml((user.name || "?").trim().charAt(0).toUpperCase());
  return `<div class="${className}">${user.avatarUrl ? `<img src="${escapeHtml(photoUrl(user.avatarUrl))}" alt="">` : initial}</div>`;
}

function socialCard(user, action = "") {
  return `<article class="social-user-card" data-social-user="${user.id}">${socialAvatar(user)}<div class="social-user-copy"><strong>${escapeHtml(user.name)}</strong><p>${escapeHtml([user.city, user.bio].filter(Boolean).join(" · ") || "Chưa có giới thiệu")}</p></div>${action}</article>`;
}

async function loadConnections(searchTerm) {
  const message = document.querySelector("#social-message");
  try {
    const [connections, users] = await Promise.all([
      request("/connections"),
      searchTerm === undefined ? Promise.resolve(null) : request(`/users/search?q=${encodeURIComponent(searchTerm)}`)
    ]);
    appState.connections = connections;
    const incoming = connections.filter((item) => item.status === "Pending" && !item.requestedByMe);
    const outgoing = connections.filter((item) => item.status === "Pending" && item.requestedByMe);
    const friends = connections.filter((item) => item.status === "Accepted");
    document.querySelector("#connection-count").textContent = friends.length;
    document.querySelector("#incoming-requests").innerHTML = incoming.length ? incoming.map((item) => socialCard(item.user, `<div><button class="social-action primary" data-connection-accept="${item.id}" type="button">Chấp nhận</button> <button class="social-action danger" data-connection-decline="${item.id}" type="button">Từ chối</button></div>`)).join("") : '<p class="empty-message">Bạn chưa có lời mời kết nối nào.</p>';
    document.querySelector("#outgoing-requests").innerHTML = outgoing.length ? outgoing.map((item) => socialCard(item.user, `<button class="social-action" data-connection-cancel="${item.id}" type="button">Chờ chấp nhận · Hủy</button>`)).join("") : '<p class="empty-message">Bạn chưa gửi lời mời nào.</p>';
    document.querySelector("#friends-list").innerHTML = friends.length ? friends.map((item) => socialCard(item.user, `<button class="social-action danger" data-connection-disconnect="${item.id}" type="button">Hủy kết nối</button>`)).join("") : '<p class="empty-message">Bạn chưa có bạn bè. Hãy tìm người để kết nối.</p>';
    if (users) {
      const statusByUser = new Map(connections.map((item) => [item.user.id, item]));
      const results = users.filter((user) => !connections.some((item) => item.user.id === user.id && ["Accepted", "Pending"].includes(item.status)));
      document.querySelector("#people-results").innerHTML = results.length ? results.map((user) => {
        const previous = statusByUser.get(user.id);
        const action = previous?.status === "Pending" ? '<button class="social-action" disabled>Đang chờ</button>' : `<button class="social-action primary" data-connection-request="${user.id}" type="button">Kết nối</button>`;
        return socialCard(user, action);
      }).join("") : '<p class="empty-message">Không tìm thấy người dùng phù hợp.</p>';
    }
    message.hidden = true;
  } catch (error) { notify(message, error.message); }
}

async function manageConnection(event) {
  const button = event.target.closest("[data-connection-request], [data-connection-accept], [data-connection-decline], [data-connection-cancel], [data-connection-disconnect]");
  if (!button) return;
  if (button.hasAttribute("data-connection-disconnect") && !window.confirm("Bạn có chắc muốn hủy kết nối này? Lịch sử tin nhắn sẽ được giữ lại nhưng không thể tiếp tục nhắn tin.")) return;
  button.disabled = true;
  try {
    if (button.dataset.connectionRequest) await request(`/connections/${button.dataset.connectionRequest}`, { method: "POST" });
    if (button.dataset.connectionAccept) await request(`/connections/${button.dataset.connectionAccept}/accept`, { method: "POST" });
    if (button.dataset.connectionDecline) await request(`/connections/${button.dataset.connectionDecline}/decline`, { method: "POST" });
    if (button.dataset.connectionCancel) await request(`/connections/${button.dataset.connectionCancel}`, { method: "DELETE" });
    if (button.dataset.connectionDisconnect) await request(`/connections/${button.dataset.connectionDisconnect}/disconnect`, { method: "POST" });
    await loadConnections(document.querySelector("#people-search-form").elements.q.value.trim());
    if (button.hasAttribute("data-connection-accept") || button.hasAttribute("data-connection-disconnect")) await loadConversations();
  } catch (error) { notify(document.querySelector("#social-message"), error.message); button.disabled = false; }
}

function renderConversations() {
  const list = document.querySelector("#conversation-list");
  const unreadTotal = appState.conversations.reduce((sum, item) => sum + item.unreadCount, 0);
  const count = document.querySelector("#message-count");
  if (count) count.textContent = unreadTotal;
  list.innerHTML = appState.conversations.length ? appState.conversations.map((item) => `<button class="conversation-item ${item.id === appState.selectedConversationId ? "is-active" : ""}" data-open-conversation="${item.id}" type="button">${socialAvatar(item.user)}<span class="conversation-copy"><strong>${escapeHtml(item.user.name)}</strong><span>${escapeHtml(item.lastMessage || "Bắt đầu cuộc trò chuyện")}</span></span>${item.unreadCount ? `<span class="unread-count">${item.unreadCount}</span>` : ""}</button>`).join("") : '<p class="chat-empty">Bạn chưa có cuộc trò chuyện. Hãy kết nối với bạn bè trước.</p>';
}

async function loadConversations() {
  try {
    appState.conversations = await request("/conversations");
    if (!appState.selectedConversationId || !appState.conversations.some((item) => item.id === appState.selectedConversationId)) {
      appState.selectedConversationId = appState.conversations[0]?.id || null;
    }
    renderConversations();
    if (appState.selectedConversationId) await loadSocialMessages();
    else {
      document.querySelector("#social-chat-heading").innerHTML = '<div class="chat-empty">Chọn một người bạn để bắt đầu trò chuyện.</div>';
      document.querySelector("#social-chat-messages").innerHTML = '<p class="chat-empty">Tin nhắn của bạn sẽ xuất hiện ở đây.</p>';
      document.querySelector("#social-chat-form").hidden = true;
    }
  } catch (error) { notify(document.querySelector("#social-chat-message"), error.message); }
}

async function loadSocialMessages() {
  const selected = appState.conversations.find((item) => item.id === appState.selectedConversationId);
  if (!selected) return;
  document.querySelector("#social-chat-heading").innerHTML = socialCard(selected.user);
  document.querySelector("#social-chat-form").hidden = false;
  try {
    const messages = await request(`/conversations/${selected.id}/messages?limit=50`);
    const container = document.querySelector("#social-chat-messages");
    container.innerHTML = messages.length ? messages.map((item) => `<article class="message-bubble ${item.senderId === appState.profile.id ? "is-mine" : ""}">${escapeHtml(item.content)}<time>${new Date(item.sentAt).toLocaleString("vi-VN", { dateStyle: "short", timeStyle: "short" })}${item.senderId === appState.profile.id ? (item.isRead ? " · Đã đọc" : " · Đã gửi") : ""}</time></article>`).join("") : '<p class="chat-empty">Chưa có tin nhắn. Hãy gửi lời chào đầu tiên!</p>';
    container.scrollTop = container.scrollHeight;
    const current = appState.conversations.find((item) => item.id === selected.id);
    if (current) current.unreadCount = 0;
    renderConversations();
  } catch (error) { notify(document.querySelector("#social-chat-message"), error.message); }
}

function startSocialPolling() {
  stopSocialPolling();
  loadConversations();
  appState.chatPoll = window.setInterval(loadConversations, 4000);
}

function stopSocialPolling() {
  if (appState.chatPoll) window.clearInterval(appState.chatPoll);
  appState.chatPoll = null;
}

async function submitSocialMessage(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const input = form.elements.content;
  const content = input.value.trim();
  if (!content || !appState.selectedConversationId) return;
  const button = form.querySelector("button");
  button.disabled = true;
  try {
    await request(`/conversations/${appState.selectedConversationId}/messages`, { method: "POST", body: JSON.stringify({ content }) });
    input.value = "";
    await loadConversations();
  } catch (error) { notify(document.querySelector("#social-chat-message"), error.message); }
  finally { button.disabled = false; }
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
  const params = new URLSearchParams({ limit: "150" });
  for (const key of ["search", "gender", "minAge", "maxAge", "city", "lookingFor", "interestId"]) {
    const value = values.get(key);
    if (value) params.set(key, value);
  }
  const radiusKm = values.get("radiusKm");
  if (radiusKm) params.set("radiusKm", radiusKm);
  const results = document.querySelector("#discover-results");
  const message = document.querySelector("#discover-message");
  const count = document.querySelector("#discover-count");
  const pagination = document.querySelector("#discover-pagination");
  message.hidden = true;
  count.textContent = "Đang tải hồ sơ...";
  pagination.replaceChildren();
  pagination.hidden = true;
  results.innerHTML = '<p class="empty-message">Đang tìm hồ sơ phù hợp...</p>';
  try {
    const profiles = await request(`/profiles/discover?${params.toString()}`);
    appState.discoverProfiles = profiles;
    appState.discoverPage = 1;
    renderDiscoverPage();
  } catch (error) {
    results.replaceChildren();
    count.textContent = "";
    appState.discoverProfiles = [];
    document.querySelector("#discover-pagination").hidden = true;
    notify(message, error.message);
  }
}

function renderDiscoverPage() {
  const results = document.querySelector("#discover-results");
  const count = document.querySelector("#discover-count");
  const pagination = document.querySelector("#discover-pagination");
  const profiles = appState.discoverProfiles;
  const pageCount = Math.max(1, Math.ceil(profiles.length / appState.discoverPageSize));
  appState.discoverPage = Math.min(Math.max(appState.discoverPage, 1), pageCount);

  if (!profiles.length) {
    results.innerHTML = '<p class="empty-message">Đã xem hết các hồ sơ trong kết quả lọc.</p>';
    count.textContent = "Đang hiển thị 0 hồ sơ";
    pagination.replaceChildren();
    pagination.hidden = true;
    return;
  }

  const start = (appState.discoverPage - 1) * appState.discoverPageSize;
  const visibleProfiles = profiles.slice(start, start + appState.discoverPageSize);
  const end = start + visibleProfiles.length;
  renderProfiles(results, visibleProfiles, true);
  count.textContent = `Hồ sơ ${start + 1}–${end} trên ${profiles.length}`;
  pagination.hidden = false;
  pagination.innerHTML = `
    <label class="discover-page-size">Mỗi trang
      <select data-discover-page-size aria-label="Số người hiển thị mỗi trang">
        <option value="10" ${appState.discoverPageSize === 10 ? "selected" : ""}>10 người</option>
        <option value="20" ${appState.discoverPageSize === 20 ? "selected" : ""}>20 người</option>
        <option value="30" ${appState.discoverPageSize === 30 ? "selected" : ""}>30 người</option>
      </select>
    </label>
    <button class="discover-page-button" type="button" data-discover-step="-1" ${appState.discoverPage === 1 ? "disabled" : ""}>Trang trước</button>
    <span>Trang ${appState.discoverPage} / ${pageCount}</span>
    <button class="discover-page-button" type="button" data-discover-step="1" ${appState.discoverPage === pageCount ? "disabled" : ""}>Trang sau</button>`;
}

function renderProfiles(container, profiles, allowSwipe, allowChat = false, swipeStatusByProfile = null) {
  if (!profiles.length) {
    container.innerHTML = '<p class="empty-message">Chưa tìm thấy hồ sơ phù hợp. Hãy thử điều chỉnh bộ lọc hoặc quay lại sau.</p>';
    return;
  }
  container.innerHTML = profiles.map((profile) => {
    const name = cleanProfileName(profile.name);
    const avatar = profile.photos?.find((photo) => photo.isAvatar) || profile.photos?.[0];
    const image = photoUrl(avatar?.url);
    const swipeStatus = swipeStatusByProfile?.get(profile.id);
    const actions = allowSwipe
      ? `<div class="person-card-actions"><button type="button" data-swipe="false">Bỏ qua</button><button type="button" data-swipe="true">Thích ♡</button><button type="button" data-swipe="true" data-super-like="true">Super Like ★</button></div>`
      : swipeStatusByProfile
        ? `<div class="person-card-actions person-card-chat-actions ${swipeStatus?.isRejected ? "rejected-actions" : swipeStatus?.isMutualLike ? "" : "pending-like-actions"}">${swipeStatus?.isRejected
          ? `<span class="rejected-status">Đã bỏ qua</span><button class="undo-swipe-button" type="button" data-undo-swipe="${profile.id}" aria-label="Thu hồi lượt bỏ qua với ${escapeHtml(name)}">Thu hồi</button>`
          : swipeStatus?.isUnmatched
            ? `<span class="rejected-status">Đã hủy kết đôi</span>`
          : swipeStatus?.isMutualLike
            ? `<button class="person-card-chat-button" type="button" data-open-chat="${profile.id}" aria-label="Nhắn tin với ${escapeHtml(name)}"><svg aria-hidden="true" viewBox="0 0 20 20"><path d="M3 4.75A2.75 2.75 0 0 1 5.75 2h8.5A2.75 2.75 0 0 1 17 4.75v5.5A2.75 2.75 0 0 1 14.25 13H9l-4.5 3v-3.2A2.75 2.75 0 0 1 3 10.25z"/></svg>Nhắn tin</button>${swipeStatus?.matchId ? `<button class="date-invite-button" type="button" data-invite-date="${profile.id}">Mời hẹn hò</button><button class="unmatch-button" type="button" data-unmatch="${swipeStatus.matchId}">Hủy kết đôi</button>` : ""}`
            : `<span class="like-pending-status">Đã thích · đang chờ phản hồi</span><button class="person-card-chat-button" type="button" data-open-chat="${profile.id}" aria-label="Nhắn tin với ${escapeHtml(name)}"><svg aria-hidden="true" viewBox="0 0 20 20"><path d="M3 4.75A2.75 2.75 0 0 1 5.75 2h8.5A2.75 2.75 0 0 1 17 4.75v5.5A2.75 2.75 0 0 1 14.25 13H9l-4.5 3v-3.2A2.75 2.75 0 0 1 3 10.25z"/></svg>Nhắn tin</button>`}</div>`
        : allowChat
          ? `<div class="person-card-actions person-card-chat-actions"><button class="person-card-chat-button" type="button" data-open-chat="${profile.id}" aria-label="Nhắn tin với ${escapeHtml(name)}"><svg aria-hidden="true" viewBox="0 0 20 20"><path d="M3 4.75A2.75 2.75 0 0 1 5.75 2h8.5A2.75 2.75 0 0 1 17 4.75v5.5A2.75 2.75 0 0 1 14.25 13H9l-4.5 3v-3.2A2.75 2.75 0 0 1 3 10.25z"/></svg>Nhắn tin</button></div>`
          : "";
    return `<article class="person-card" data-profile-id="${profile.id}">
      ${image ? `<img class="person-photo" src="${escapeHtml(image)}" alt="Ảnh hồ sơ ${escapeHtml(name)}">` : `<div class="person-photo person-photo-placeholder" aria-hidden="true">${escapeHtml(name.trim().charAt(0).toUpperCase())}</div>`}
      <div class="person-card-body"><div class="person-card-heading"><h2>${escapeHtml(name)}</h2><span>${ageFrom(profile.birthDate)}</span></div>
      ${profile.isOnline ? '<span class="online-status">Đang hoạt động</span>' : ""}
      <div class="person-meta">${profile.city ? `<span>${escapeHtml(profile.city)}</span>` : ""}${Number.isFinite(profile.distanceKm) ? `<span>${Math.round(profile.distanceKm)} km</span>` : ""}<span>${escapeHtml(profile.lookingFor || "Dating")}</span></div>
      <p class="person-bio">${escapeHtml(profile.bio || "Chưa có phần giới thiệu.")}</p>
      <div class="person-interests">${(profile.interests || []).map((interest) => `<span class="interest-chip">${escapeHtml(interest.name)}</span>`).join("")}</div>
      ${actions}<div class="person-safety-actions"><button type="button" data-report-user="${profile.id}" data-user-name="${escapeHtml(name)}">Báo cáo</button><button type="button" data-block-user="${profile.id}" data-user-name="${escapeHtml(name)}">Chặn</button></div></div></article>`;
  }).join("");
}

async function loadProfile() {
  try {
    appState.profile = await request("/profiles/me");
    fillProfile();
    renderPhotos();
    await Promise.all([loadPrivacySettings(), loadNotificationPreferences(), loadSessions(), loadBlockedUsers(), loadEmailVerificationStatus()]);
  } catch (error) {
    notify(document.querySelector("#profile-message"), error.message);
  }
}

function suggestProfileImprovements() {
  const form = document.querySelector("#profile-form");
  const suggestions = [];
  if (form.elements.bio.value.trim().length < 100) suggestions.push("Viết thêm vài câu về điều bạn thích làm và một điều bạn muốn cùng người khác trải nghiệm.");
  if ((appState.profile?.interests || []).length < 3) suggestions.push("Chọn ít nhất 3 sở thích để người khác dễ tìm chủ đề chung.");
  if ((appState.profile?.photos || []).length < 2) suggestions.push("Thêm một vài ảnh rõ mặt và ảnh hoạt động thường ngày.");
  if (!form.elements.city.value.trim()) suggestions.push("Thêm thành phố để hồ sơ xuất hiện trong tìm kiếm gần bạn.");
  if (!suggestions.length) suggestions.push("Hồ sơ đã có thông tin cơ bản. Hãy giữ phần giới thiệu cụ thể và cập nhật ảnh mới khi phù hợp.");
  notify(document.querySelector("#profile-message"), suggestions.join(" "), true);
}

async function loadPrivacySettings() {
  const settings = await request("/profiles/me/privacy");
  const form = document.querySelector("#privacy-form");
  for (const key of ["hideAge", "hideCity", "hideOccupation", "hideEducation", "isIncognito"]) {
    form.elements[key].checked = Boolean(settings[key]);
  }
}

async function loadNotificationPreferences() {
  const preferences = await request("/notifications/preferences");
  const form = document.querySelector("#notification-preferences-form");
  for (const key of ["likeEnabled", "matchEnabled", "chatEnabled", "commentEnabled", "dateEnabled"]) {
    form.elements[key].checked = Boolean(preferences[key]);
  }
}

async function saveNotificationPreferences(event) {
  event.preventDefault();
  const values = new FormData(event.currentTarget);
  const preferences = Object.fromEntries(["likeEnabled", "matchEnabled", "chatEnabled", "commentEnabled", "dateEnabled"]
    .map((key) => [key, values.has(key)]));
  const message = document.querySelector("#notification-preferences-message");
  try {
    await request("/notifications/preferences", { method: "PUT", body: JSON.stringify(preferences) });
    notify(message, "Đã lưu cài đặt thông báo.", true);
  } catch (error) {
    notify(message, error.message);
  }
}

async function loadNotifications() {
  try {
    const notifications = await request("/notifications?limit=100", { cache: "no-store" });
    if (appState.notificationsInitialized && "Notification" in window && Notification.permission === "granted") {
      for (const item of notifications) {
        if (!appState.notificationIds.has(item.id) && !item.readAt) new Notification(item.title, { body: item.body, tag: item.id });
      }
    }
    appState.notificationsInitialized = true;
    appState.notifications = notifications;
    appState.notificationIds = new Set(notifications.map((item) => item.id));
    const unread = notifications.filter((item) => !item.readAt).length;
    document.querySelector("#notification-count").textContent = unread;
    renderNotifications();
  } catch (error) {
    notify(document.querySelector("#notification-list"), `Không tải được thông báo: ${error.message}. Hãy thử làm mới.`, false);
  }
}

function renderNotifications() {
  const list = document.querySelector("#notification-list");
  const typeLabels = { Like: "Lượt thích", SuperLike: "Super Like", Match: "Kết đôi", Chat: "Tin nhắn", Comment: "Bình luận", Date: "Lịch hẹn", Event: "Sự kiện", System: "Hệ thống" };
  list.innerHTML = appState.notifications.length ? appState.notifications.map((item) => `
    <article class="notification-card ${item.readAt ? "is-read" : "is-unread"}">
      <div><span class="notification-type">${escapeHtml(typeLabels[item.type] || item.type)}</span><h2>${escapeHtml(item.title)}</h2><p>${escapeHtml(item.body)}</p><time>${new Date(item.createdAt).toLocaleString("vi-VN", { dateStyle: "medium", timeStyle: "short" })}</time></div>
      ${item.readAt ? "" : `<button type="button" data-mark-notification="${item.id}">Đánh dấu đã đọc</button>`}
    </article>`).join("") : '<p class="empty-message">Chưa có thông báo mới.</p>';
}

async function manageNotifications(event) {
  const button = event.target.closest("[data-mark-notification]");
  if (!button) return;
  button.disabled = true;
  try {
    await request(`/notifications/${button.dataset.markNotification}/read`, { method: "POST" });
    await loadNotifications();
  } catch (error) {
    button.disabled = false;
    notify(document.querySelector("#notification-list"), error.message);
  }
}

async function markAllNotificationsRead() {
  try {
    await request("/notifications/read-all", { method: "POST" });
    await loadNotifications();
  } catch (error) {
    notify(document.querySelector("#notification-list"), error.message);
  }
}

async function enableBrowserNotifications() {
  if (!("Notification" in window)) {
    notify(document.querySelector("#notification-list"), "Trình duyệt này không hỗ trợ thông báo.");
    return;
  }
  const permission = await Notification.requestPermission();
  if (permission === "granted") notify(document.querySelector("#notification-list"), "Thông báo trình duyệt đã bật khi Kiss đang mở.", true);
}

async function loadDates() {
  const list = document.querySelector("#date-list");
  const invitationForm = document.querySelector("#date-invitation-form");
  const recipientSelect = invitationForm.elements.recipientUserId;
  const isAdmin = appState.isOwner;
  invitationForm.closest(".date-planning-grid").classList.toggle("is-admin", isAdmin);
  invitationForm.hidden = isAdmin;
  document.querySelector("#dates-description").textContent = isAdmin
    ? "Toàn bộ lịch hẹn giữa các thành viên trong hệ thống."
    : "Gửi lời mời đến người bạn đã kết đôi, chọn địa điểm và thời gian phù hợp.";
  document.querySelector("#dates-list-heading").textContent = isAdmin ? "Tất cả lịch hẹn" : "Lời mời và lịch sắp tới";
  try {
    const dates = isAdmin ? await request("/admin/dates") : await request("/dates");
    if (!isAdmin) {
      const matches = await request("/matches");
      recipientSelect.innerHTML = '<option value="">Chọn người</option>' + matches.map((match) =>
        `<option value="${escapeHtml(match.profile.id)}">${escapeHtml(match.profile.name)}</option>`).join("");
      if (appState.pendingDateRecipientId && matches.some((match) => match.profile.id === appState.pendingDateRecipientId)) {
        recipientSelect.value = appState.pendingDateRecipientId;
        appState.pendingDateRecipientId = null;
      }
    }
    appState.dates = dates;
    const statusLabels = { Pending: "Chờ phản hồi", Accepted: "Đã xác nhận", Declined: "Đã từ chối", Cancelled: "Đã hủy" };
    list.innerHTML = dates.length ? dates.map((date) => {
      const startsAt = new Date(date.startsAt);
      const isPast = startsAt <= new Date();
      const safeUrl = (() => { try { const url = new URL(date.venueUrl); return ["http:", "https:"].includes(url.protocol) ? url.href : ""; } catch { return ""; } })();
      const reviews = date.reviews.map((review) => `<div class="date-review"><strong>${escapeHtml(review.reviewerName)}</strong><span>${"★".repeat(review.rating)}${"☆".repeat(5 - review.rating)}</span><p>${escapeHtml(review.comment || "Không có nhận xét.")}</p></div>`).join("");
      const actions = isAdmin ? "" : `${!date.isOrganizer && date.status === "Pending" ? `<button type="button" data-date-action="accept" data-date-id="${date.id}">Đồng ý</button><button type="button" data-date-action="decline" data-date-id="${date.id}">Từ chối</button>` : ""}${["Pending", "Accepted"].includes(date.status) ? `<button type="button" class="cancel-date-button" data-cancel-date="${date.id}">Hủy lịch</button>` : ""}`;
      const reviewForm = !isAdmin && date.status === "Accepted" && isPast && !date.hasReviewed ? `<form class="date-review-form" data-date-review="${date.id}"><label class="field"><span>Đánh giá buổi hẹn</span><select name="rating"><option value="5">5 sao</option><option value="4">4 sao</option><option value="3">3 sao</option><option value="2">2 sao</option><option value="1">1 sao</option></select></label><label class="field"><span>Nhận xét</span><input name="comment" maxlength="500" placeholder="Chia sẻ cảm nhận"></label><button class="quiet-button" type="submit">Gửi đánh giá</button></form>` : "";
      const people = isAdmin ? `${escapeHtml(date.organizerName)} → ${escapeHtml(date.recipientName)}` : escapeHtml(date.otherUserName);
      return `<article class="date-card"><header><div><span class="date-status">${escapeHtml(statusLabels[date.status] || date.status)}</span><h3>${escapeHtml(date.venueName)}</h3></div><strong>${people}</strong></header><p class="date-time">${startsAt.toLocaleString("vi-VN", { dateStyle: "full", timeStyle: "short" })}</p>${date.venueAddress ? `<p>${escapeHtml(date.venueAddress)}</p>` : ""}${safeUrl ? `<a href="${escapeHtml(safeUrl)}" target="_blank" rel="noreferrer">Mở địa điểm</a>` : ""}${date.message ? `<p class="date-message">${escapeHtml(date.message)}</p>` : ""}<div class="date-card-actions">${actions}</div>${reviewForm}<div class="date-reviews">${reviews}</div></article>`;
    }).join("") : isAdmin ? '<p class="empty-message">Chưa có lịch hẹn nào trong hệ thống.</p>' : '<p class="empty-message">Chưa có lời mời hẹn hò. Bạn có thể gửi lời mời cho người đã kết đôi với mình.</p>';
  } catch (error) {
    notify(list, `Không tải được lịch hẹn: ${error.message}`);
  }
}

async function submitDateInvitation(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const values = Object.fromEntries(new FormData(form));
  const message = document.querySelector("#date-form-message");
  values.startsAt = new Date(values.startsAt).toISOString();
  try {
    await request("/dates", { method: "POST", body: JSON.stringify(values) });
    form.reset();
    notify(message, "Đã gửi lời mời hẹn hò.", true);
    await loadDates();
    await loadNotifications();
  } catch (error) {
    notify(message, error.message);
  }
}

async function manageDateInvitation(event) {
  const actionButton = event.target.closest("[data-date-action]");
  const cancelButton = event.target.closest("[data-cancel-date]");
  const button = actionButton || cancelButton;
  if (!button) return;
  button.disabled = true;
  try {
    if (actionButton) {
      await request(`/dates/${button.dataset.dateId}/${button.dataset.dateAction}`, { method: "POST" });
    } else {
      if (!window.confirm("Bạn có muốn hủy lịch hẹn này không?")) { button.disabled = false; return; }
      await request(`/dates/${button.dataset.cancelDate}`, { method: "DELETE" });
    }
    await loadDates();
    await loadNotifications();
  } catch (error) {
    button.disabled = false;
    notify(document.querySelector("#date-form-message"), error.message);
  }
}

async function submitDateReview(event) {
  const form = event.target.closest("[data-date-review]");
  if (!form) return;
  event.preventDefault();
  try {
    await request(`/dates/${form.dataset.dateReview}/reviews`, {
      method: "POST",
      body: JSON.stringify({ rating: Number(new FormData(form).get("rating")), comment: new FormData(form).get("comment") })
    });
    await loadDates();
  } catch (error) {
    notify(document.querySelector("#date-form-message"), error.message);
  }
}

async function loadCommunityEvents() {
  const container = document.querySelector("#community-event-list");
  try {
    const events = await request("/events?limit=100");
    container.innerHTML = events.length ? events.map((item) => `
      <article class="community-event-card">
        <header><div><span class="date-status">${new Date(item.startsAt).toLocaleString("vi-VN", { dateStyle: "medium", timeStyle: "short" })}</span><h3>${escapeHtml(item.name)}</h3></div><strong>${item.attendeeCount} người tham gia</strong></header>
        <p><b>Địa điểm:</b> ${escapeHtml(item.venueName)}</p>
        ${item.description ? `<p>${escapeHtml(item.description)}</p>` : ""}
        <small>Tạo bởi ${escapeHtml(item.creatorName)}</small>
        <div class="date-card-actions"><button class="cancel-date-button" type="button" data-delete-community-event="${item.id}">Xóa sự kiện</button></div>
      </article>`).join("") : '<p class="empty-message">Chưa có sự kiện sắp tới. Bạn có thể tạo sự kiện đầu tiên.</p>';
  } catch (error) {
    container.innerHTML = `<p class="empty-message">${escapeHtml(error.message)}</p>`;
  }
}

async function submitCommunityEvent(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const values = new FormData(form);
  const button = form.querySelector("[type=submit]");
  button.disabled = true;
  try {
    await request("/events", {
      method: "POST",
      body: JSON.stringify({
        name: values.get("name").trim(),
        venueName: values.get("venueName").trim(),
        description: values.get("description").trim(),
        startsAt: new Date(values.get("startsAt")).toISOString()
      })
    });
    form.reset();
    notify(document.querySelector("#community-event-message"), "Đã đăng sự kiện.", true);
    await loadCommunityEvents();
  } catch (error) {
    notify(document.querySelector("#community-event-message"), error.message);
  } finally {
    button.disabled = false;
  }
}

async function manageCommunityEvent(event) {
  const deleteButton = event.target.closest("[data-delete-community-event]");
  const attendanceButton = event.target.closest("[data-toggle-event-attendance]");
  if (!deleteButton && !attendanceButton) return;
  const eventId = deleteButton?.dataset.deleteCommunityEvent || attendanceButton.dataset.toggleEventAttendance;
  try {
    if (deleteButton) {
      if (!window.confirm("Xóa sự kiện này?")) return;
      await request(`/events/${eventId}`, { method: "DELETE" });
    } else {
      await request(`/events/${eventId}/attend`, { method: attendanceButton.dataset.attending === "true" ? "DELETE" : "POST" });
    }
    await loadCommunityEvents();
  } catch (error) {
    notify(document.querySelector("#community-event-message"), error.message);
  }
}

async function savePrivacySettings(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const values = new FormData(form);
  const settings = Object.fromEntries(["hideAge", "hideCity", "hideOccupation", "hideEducation", "isIncognito"]
    .map((key) => [key, values.has(key)]));
  const message = document.querySelector("#privacy-message");
  try {
    await request("/profiles/me/privacy", { method: "PUT", body: JSON.stringify(settings) });
    if (!document.querySelector("#discover-page").hidden) await loadDiscover();
    notify(message, "Đã lưu quyền riêng tư.", true);
  } catch (error) {
    notify(message, error.message);
  }
}

async function submitPasswordChange(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const message = document.querySelector("#change-password-message");
  try {
    await request("/auth/change-password", {
      method: "POST",
      body: JSON.stringify(Object.fromEntries(new FormData(form)))
    });
    form.reset();
    notify(message, "Đã đổi mật khẩu. Các thiết bị khác đã được đăng xuất.", true);
    await loadSessions();
  } catch (error) {
    notify(message, error.message);
  }
}

async function loadSessions() {
  const container = document.querySelector("#session-list");
  const sessions = await request("/auth/sessions");
  if (!sessions.length) {
    container.innerHTML = '<p class="session-empty">Không có phiên đăng nhập đang hoạt động.</p>';
    return;
  }
  container.innerHTML = sessions.map((session) => `
    <div class="session-row"><div><strong>${escapeHtml(session.deviceName)}</strong><small>Hoạt động ${new Date(session.lastSeenAt).toLocaleString("vi-VN", { dateStyle: "short", timeStyle: "short" })}${session.isCurrent ? " · Thiết bị này" : ""}</small></div>
    ${session.isCurrent ? "" : `<button type="button" data-revoke-session="${escapeHtml(session.id)}">Đăng xuất</button>`}</div>`).join("");
}

async function loadBlockedUsers() {
  const container = document.querySelector("#blocked-users");
  const users = await request("/safety/blocks");
  container.innerHTML = users.length ? users.map((user) => `
    <div class="session-row"><div><strong>${escapeHtml(user.name)}</strong><small>Đã chặn ${new Date(user.createdAt).toLocaleDateString("vi-VN")}</small></div>
    <button type="button" data-unblock-user="${escapeHtml(user.userId)}">Bỏ chặn</button></div>`).join("")
    : '<p class="session-empty">Bạn chưa chặn ai.</p>';
}

async function blockUser(userId, name = "người dùng này") {
  if (!window.confirm(`Chặn ${name}? Hai bên sẽ không thể liên hệ hoặc thấy hoạt động của nhau.`)) return;
  try {
    await request(`/safety/users/${userId}/block`, { method: "POST" });
    if (chatState.activeUserId === userId) clearActiveChat();
    await Promise.all([loadDiscover(), loadJourney(), loadChatContacts(), loadBlockedUsers()]);
    notify(document.querySelector("#change-password-message"), `Đã chặn ${name}.`, true);
  } catch (error) {
    notify(document.querySelector("#change-password-message"), error.message);
  }
}

async function unblockUser(userId) {
  try {
    await request(`/safety/users/${userId}/block`, { method: "DELETE" });
    await Promise.all([loadBlockedUsers(), loadDiscover(), loadJourney(), loadChatContacts()]);
  } catch (error) {
    notify(document.querySelector("#change-password-message"), error.message);
  }
}

async function reportContent(targetType, targetId) {
  const reason = window.prompt("Vui lòng cho biết lý do báo cáo:");
  if (reason === null) return;
  const route = { User: "users", Post: "posts", Comment: "comments", Message: "messages" }[targetType];
  if (!route || !reason.trim()) return;
  const message = document.querySelector(targetType === "Message" ? "#chat-status" : "#journey-message");
  try {
    await request(`/safety/${route}/${targetId}/reports`, { method: "POST", body: JSON.stringify({ reason: reason.trim() }) });
    notify(message, "Đã gửi báo cáo để quản trị viên xem xét.", true);
  } catch (error) {
    notify(message, error.message);
  }
}

async function loadEmailVerificationStatus() {
  const status = await request("/auth/email-verification");
  document.querySelector("#email-verification-status").textContent = status.isVerified
    ? `Email đã được xác minh${status.verifiedAt ? ` vào ${new Date(status.verifiedAt).toLocaleDateString("vi-VN")}` : ""}.`
    : "Email chưa được xác minh.";
  document.querySelector("#request-email-code").hidden = status.isVerified;
  document.querySelector("#email-verification-form").hidden = status.isVerified;
}

async function requestEmailVerificationCode() {
  const button = document.querySelector("#request-email-code");
  const message = document.querySelector("#email-verification-message");
  button.disabled = true;
  try {
    await request("/auth/email-verification/request", { method: "POST" });
    notify(message, "Nếu email chưa được xác minh, mã OTP sẽ được gửi đến hộp thư của bạn.", true);
  } catch (error) {
    notify(message, error.message);
  } finally {
    button.disabled = false;
  }
}

async function submitEmailVerification(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const message = document.querySelector("#email-verification-message");
  try {
    await request("/auth/email-verification/verify", {
      method: "POST",
      body: JSON.stringify(Object.fromEntries(new FormData(form)))
    });
    form.reset();
    await loadEmailVerificationStatus();
    notify(message, "Email đã được xác minh.", true);
  } catch (error) {
    notify(message, error.message);
  }
}

async function manageSessions(event) {
  const button = event.target.closest("[data-revoke-session]");
  if (!button) return;
  button.disabled = true;
  try {
    await request(`/auth/sessions/${button.dataset.revokeSession}`, { method: "DELETE" });
    await loadSessions();
  } catch (error) {
    button.disabled = false;
    notify(document.querySelector("#change-password-message"), error.message);
  }
}

async function revokeOtherSessions() {
  const button = document.querySelector("#revoke-other-sessions");
  button.disabled = true;
  try {
    const result = await request("/auth/sessions/revoke-others", { method: "POST" });
    await loadSessions();
    notify(document.querySelector("#change-password-message"), `Đã đăng xuất ${result.revokedCount} thiết bị khác.`, true);
  } catch (error) {
    notify(document.querySelector("#change-password-message"), error.message);
  } finally {
    button.disabled = false;
  }
}

function updateDiscoveryLocation() {
  const message = document.querySelector("#location-message");
  if (!navigator.geolocation) {
    notify(message, "Trình duyệt này không hỗ trợ định vị.");
    return;
  }
  const button = document.querySelector("#discover-location-button");
  button.disabled = true;
  navigator.geolocation.getCurrentPosition(async ({ coords }) => {
    try {
      await request("/profiles/me/location", {
        method: "PUT",
        body: JSON.stringify({ latitude: coords.latitude, longitude: coords.longitude })
      });
      notify(message, "Đã lưu vị trí riêng tư. Bạn có thể lọc theo khoảng cách.", true);
    } catch (error) {
      notify(message, error.message);
    } finally {
      button.disabled = false;
    }
  }, (error) => {
    const detail = error.code === error.PERMISSION_DENIED ? "Bạn chưa cho phép truy cập vị trí." : "Không thể lấy vị trí hiện tại.";
    notify(message, detail);
    button.disabled = false;
  }, { enableHighAccuracy: false, timeout: 10000, maximumAge: 300000 });
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
      body: JSON.stringify({ targetUserId: card.dataset.profileId, isLike: button.dataset.swipe === "true", isSuperLike: button.dataset.superLike === "true" })
    });
    appState.discoverProfiles = appState.discoverProfiles.filter((profile) => profile.id !== card.dataset.profileId);
    renderDiscoverPage();
    if (result.isLike) await loadLikedProfiles();
    else await loadRejectedProfiles();
    if (result.isMatch) {
      notify(document.querySelector("#discover-message"), result.isSuperLike ? "Super Like đã tạo kết đôi!" : "Hai bạn đã thích nhau! Đã có một người bạn mới.", true);
    }
  } catch (error) {
    buttons.forEach((item) => { item.disabled = false; });
    notify(document.querySelector("#discover-message"), error.message);
  }
}

async function loadLikedProfiles() {
  try {
    const likes = await request("/swipes/liked");
    document.querySelector("#match-count").textContent = likes.length;
    const results = document.querySelector("#matches-results");
    if (!likes.length) {
      results.innerHTML = '<p class="empty-message">Bạn chưa thích hồ sơ nào. Hãy khám phá và chọn Thích để lưu hồ sơ ở đây.</p>';
      return;
    }
    const statusByProfile = new Map(likes.map((item) => [item.profile.id, item]));
    renderProfiles(results, likes.map((item) => item.profile), false, false, statusByProfile);
  } catch (error) {
    notify(document.querySelector("#discover-message"), error.message);
  }
}

async function loadRejectedProfiles() {
  const message = document.querySelector("#passed-message");
  const results = document.querySelector("#passed-results");
  try {
    const rejected = await request("/swipes/rejected");
    document.querySelector("#passed-count").textContent = rejected.length;
    message.hidden = true;
    if (!rejected.length) {
      results.innerHTML = '<p class="empty-message">Bạn chưa bỏ qua hồ sơ nào. Hồ sơ bạn chọn Bỏ qua sẽ được lưu ở đây.</p>';
      return;
    }
    const statusByProfile = new Map(rejected.map((item) => [item.profile.id, { isRejected: true }]));
    renderProfiles(results, rejected.map((item) => item.profile), false, false, statusByProfile);
  } catch (error) {
    notify(message, error.message);
  }
}

async function undoPass(event) {
  const button = event.target.closest("[data-undo-swipe]");
  if (!button) return;
  button.disabled = true;
  try {
    await request(`/swipes/${button.dataset.undoSwipe}`, { method: "DELETE" });
    await Promise.all([loadRejectedProfiles(), loadDiscover()]);
    notify(document.querySelector("#passed-message"), "Đã thu hồi lượt bỏ qua. Hồ sơ sẽ xuất hiện lại trong Khám phá.", true);
  } catch (error) {
    notify(document.querySelector("#passed-message"), error.message);
    button.disabled = false;
  }
}

async function openChatFromMatch(event) {
  const dateButton = event.target.closest("[data-invite-date]");
  if (dateButton && document.querySelector("#matches-page").contains(dateButton)) {
    appState.pendingDateRecipientId = dateButton.dataset.inviteDate;
    showPage("dates");
    return;
  }
  const unmatchButton = event.target.closest("[data-unmatch]");
  if (unmatchButton && document.querySelector("#matches-page").contains(unmatchButton)) {
    if (!window.confirm("Bạn có chắc muốn hủy kết đôi này không?")) return;
    unmatchButton.disabled = true;
    try {
      await request(`/matches/${unmatchButton.dataset.unmatch}`, { method: "DELETE" });
      await loadLikedProfiles();
      await loadChatContacts();
    } catch (error) {
      unmatchButton.disabled = false;
      notify(document.querySelector("#discover-message"), error.message);
    }
    return;
  }
  const button = event.target.closest("[data-open-chat]");
  if (!button || !document.querySelector("#matches-page").contains(button)) return;
  chatState.pendingUserId = button.dataset.openChat;
  showPage("chat");
}

async function loadChatContacts() {
  const list = document.querySelector("#chat-contact-list");
  const status = document.querySelector("#chat-status");
  try {
    const contacts = await request("/chat/contacts");
    chatState.contacts = contacts;
    document.querySelector("#chat-contact-count").textContent = contacts.length;

    if (!chatState.contacts.length) {
      list.innerHTML = '<p class="chat-contact-empty">Bạn chưa có cuộc trò chuyện. Hãy thích một hồ sơ để bắt đầu nhắn tin.</p>';
      chatState.pendingUserId = null;
      clearActiveChat();
      return;
    }

    renderChatContacts();
    const preferredId = chatState.pendingUserId || chatState.activeUserId || chatState.contacts[0].id;
    chatState.pendingUserId = null;
    const preferredContact = chatState.contacts.find((profile) => profile.id === preferredId);
    if (preferredContact) {
      await selectChatContact(preferredContact.id);
    } else {
      clearActiveChat();
    }
  } catch (error) {
    list.innerHTML = `<p class="chat-contact-empty">${escapeHtml(error.message)}</p>`;
    notify(status, error.message);
  }
}

function renderChatContacts() {
  const list = document.querySelector("#chat-contact-list");
  list.innerHTML = chatState.contacts.map((profile) => {
    const name = cleanProfileName(profile.name);
    const avatar = profile.photos?.find((photo) => photo.isAvatar) || profile.photos?.[0];
    const image = photoUrl(avatar?.url);
    const initials = (name || "K").trim().charAt(0).toUpperCase();
    return `<button class="chat-contact ${chatState.activeUserId === profile.id ? "is-active" : ""}" data-chat-user="${profile.id}" type="button">
      ${image ? `<img class="chat-contact-avatar" src="${escapeHtml(image)}" alt="">` : `<span class="chat-contact-avatar chat-contact-initial">${escapeHtml(initials)}</span>`}
      <span class="chat-contact-copy"><strong>${escapeHtml(name)}</strong><small>${profile.isOnline ? '<span class="online-dot" aria-hidden="true"></span> Đang hoạt động' : "Nhấn để trò chuyện"}</small></span><span class="chat-contact-arrow" aria-hidden="true">›</span>
    </button>`;
  }).join("");
}

async function selectChatContact(userId) {
  const profile = chatState.contacts.find((item) => item.id === userId);
  if (!profile) return;
  const changed = chatState.activeUserId !== userId;
  if (changed) {
    clearChatMediaUrls();
    chatState.messages = [];
    chatState.lastMessageKey = null;
    document.querySelector("#chat-form").reset();
    updateChatFileName();
  }
  chatState.activeUserId = userId;
  document.querySelector("#chat-empty").hidden = true;
  document.querySelector("#chat-thread-header").hidden = false;
  document.querySelector("#chat-form").hidden = false;
  const name = cleanProfileName(profile.name);
  document.querySelector("#chat-peer-name").textContent = name;
  document.querySelector("#chat-peer-block").dataset.blockUser = userId;
  const avatar = profile.photos?.find((photo) => photo.isAvatar) || profile.photos?.[0];
  const image = photoUrl(avatar?.url);
  const avatarElement = document.querySelector("#chat-peer-avatar");
  avatarElement.innerHTML = image
    ? `<img src="${escapeHtml(image)}" alt="">`
    : escapeHtml((name || "K").trim().charAt(0).toUpperCase());
  document.querySelector("#chat-status").hidden = true;
  renderChatContacts();
  await loadChatMessages(changed);
  if (!document.querySelector("#chat-page").hidden) startChatPolling();
}

function clearActiveChat() {
  stopChatPolling();
  chatState.activeUserId = null;
  chatState.messages = [];
  chatState.lastMessageKey = null;
  clearChatMediaUrls();
  document.querySelector("#chat-empty").hidden = false;
  document.querySelector("#chat-thread-header").hidden = true;
  document.querySelector("#chat-form").hidden = true;
  document.querySelector("#chat-form").reset();
  updateChatFileName();
  document.querySelector("#chat-status").hidden = true;
  document.querySelector("#chat-messages").replaceChildren();
}

function clearChatMediaUrls() {
  for (const url of chatState.mediaUrls.values()) {
    if (typeof url === "string" && url.startsWith("blob:")) URL.revokeObjectURL(url);
  }
  chatState.mediaUrls.clear();
}

function stopChatPolling() {
  if (chatState.pollTimer) clearInterval(chatState.pollTimer);
  chatState.pollTimer = null;
}

function startChatPolling() {
  stopChatPolling();
  if (!chatState.activeUserId) return;
  chatState.pollTimer = setInterval(() => loadChatMessages(false), 3000);
}

async function loadChatAttachment(message) {
  if (!message.mediaUrl || chatState.mediaUrls.has(message.id)) return;
  const conversationId = chatState.activeUserId;
  chatState.mediaUrls.set(message.id, null);
  try {
    const headers = new Headers();
    if (appState.token) headers.set("Authorization", `Bearer ${appState.token}`);
    const response = await fetch(message.mediaUrl, { headers });
    if (!response.ok) throw new Error("Không tải được tệp đính kèm.");
    const objectUrl = URL.createObjectURL(await response.blob());
    if (chatState.activeUserId !== conversationId) {
      URL.revokeObjectURL(objectUrl);
      return;
    }
    chatState.mediaUrls.set(message.id, objectUrl);
  } catch {
    if (chatState.activeUserId === conversationId) chatState.mediaUrls.set(message.id, "");
  }
}

async function loadChatMessages(scrollToLatest = false) {
  if (!chatState.activeUserId || chatState.isLoading) return;
  chatState.isLoading = true;
  const container = document.querySelector("#chat-messages");
  const conversationId = chatState.activeUserId;
  const wasNearBottom = container.scrollHeight - container.scrollTop - container.clientHeight < 80;
  try {
    const messages = await request(`/chat/${conversationId}/messages`);
    if (chatState.activeUserId !== conversationId) return;
    const key = messages.map((message) => `${message.id}:${message.content}:${message.isRead}:${message.isRevoked}`).join("|");
    if (key === chatState.lastMessageKey) return;
    chatState.messages = messages;
    await Promise.all(messages.map(loadChatAttachment));
    chatState.lastMessageKey = key;
    renderChatMessages(scrollToLatest || wasNearBottom);
  } catch (error) {
    notify(document.querySelector("#chat-status"), error.message);
  } finally {
    chatState.isLoading = false;
  }
}

function renderChatMessages(scrollToLatest) {
  const container = document.querySelector("#chat-messages");
  if (!chatState.messages.length) {
    container.innerHTML = '<p class="chat-no-messages">Chưa có tin nhắn. Hãy gửi lời chào đầu tiên nhé.</p>';
    return;
  }

  container.innerHTML = chatState.messages.map((message) => {
    const mediaUrl = chatState.mediaUrls.get(message.id);
    const mediaMarkup = !message.isRevoked && message.mediaUrl
      ? mediaUrl
        ? message.mediaType?.startsWith("video/")
          ? `<video class="chat-message-media" src="${escapeHtml(mediaUrl)}" controls playsinline preload="metadata"></video>`
          : `<img class="chat-message-media" src="${escapeHtml(mediaUrl)}" alt="Ảnh được gửi trong cuộc trò chuyện">`
        : `<span class="chat-attachment-status">${mediaUrl === "" ? "Không tải được tệp đính kèm." : "Đang tải tệp..."}</span>`
      : "";
    const time = new Date(message.sentAt).toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
    return `<article class="chat-message-row ${message.isMine ? "is-mine" : ""}" data-chat-message="${message.id}">
      <div class="chat-message-bubble ${message.isRevoked ? "is-revoked" : ""}">${message.content ? `<p>${escapeHtml(message.content)}</p>` : ""}${mediaMarkup}<time>${time}${message.isMine ? (message.isRead ? " · Đã xem" : " · Đã gửi") : ""}</time>
      <div class="chat-message-actions">${message.canRecall ? `<button type="button" data-recall-message="${message.id}">Thu hồi</button>` : ""}<button type="button" data-hide-message="${message.id}">Xóa ở phía tôi</button>${!message.isMine ? `<button type="button" data-report-message="${message.id}">Báo cáo</button>` : ""}</div></div>
    </article>`;
  }).join("");
  if (scrollToLatest) container.scrollTop = container.scrollHeight;
}

async function manageChatMessage(event) {
  const recallButton = event.target.closest("[data-recall-message]");
  const hideButton = event.target.closest("[data-hide-message]");
  const button = recallButton || hideButton;
  if (!button) return;
  button.disabled = true;
  try {
    if (recallButton) {
      await request(`/chat/messages/${button.dataset.recallMessage}/recall`, { method: "POST" });
    } else {
      await request(`/chat/messages/${button.dataset.hideMessage}`, { method: "DELETE" });
    }
    chatState.lastMessageKey = null;
    await loadChatMessages(false);
  } catch (error) {
    button.disabled = false;
    notify(document.querySelector("#chat-status"), error.message);
  }
}

function updateChatFileName() {
  const input = document.querySelector("#chat-file");
  const label = document.querySelector("#chat-file-name");
  const clearButton = document.querySelector("#chat-file-clear");
  const file = input.files?.[0];
  label.textContent = file ? file.name : "Tối đa 8 MB cho ảnh, 20 MB cho video";
  clearButton.hidden = !file;
}

function toggleChatGiftPicker() {
  const picker = document.querySelector("#chat-gift-picker");
  picker.hidden = !picker.hidden;
  document.querySelector("#chat-suggestion-picker").hidden = true;
}

function sendChatGift(event) {
  const button = event.target.closest("[data-chat-gift]");
  if (!button) return;
  document.querySelector("#chat-gift-picker").hidden = true;
  document.querySelector("#chat-content").value = `🎁 Gửi tặng bạn: ${button.dataset.chatGift}!`;
  document.querySelector("#chat-form").requestSubmit();
}

function showChatSuggestions() {
  if (!chatState.activeUserId) return;
  const picker = document.querySelector("#chat-suggestion-picker");
  picker.hidden = !picker.hidden;
  document.querySelector("#chat-gift-picker").hidden = true;
  if (picker.hidden) return;
  const profile = chatState.contacts.find((item) => item.id === chatState.activeUserId);
  const name = cleanProfileName(profile?.name || "bạn");
  const sharedInterest = (profile?.interests || []).find((interest) => appState.profile?.interests?.some((mine) => mine.id === interest.id));
  const suggestions = sharedInterest
    ? [`Chào ${name}! Mình thấy chúng ta cùng thích ${sharedInterest.name}. Bạn bắt đầu sở thích đó từ khi nào?`, `Bạn thường thích làm gì nhất liên quan đến ${sharedInterest.name}?`, `Cuối tuần này bạn muốn thử hoạt động nào mới?`]
    : [`Chào ${name}! Điều gì làm bạn vui nhất hôm nay?`, `Nếu cuối tuần được đi đâu đó, bạn sẽ chọn nơi nào?`, `Gần đây bạn có bộ phim hay món ăn nào muốn giới thiệu không?`];
  picker.innerHTML = suggestions.map((suggestion) => `<button type="button" data-chat-suggestion="${escapeHtml(suggestion)}">${escapeHtml(suggestion)}</button>`).join("");
}

function selectChatSuggestion(event) {
  const button = event.target.closest("[data-chat-suggestion]");
  if (!button) return;
  document.querySelector("#chat-content").value = button.dataset.chatSuggestion;
  document.querySelector("#chat-suggestion-picker").hidden = true;
  document.querySelector("#chat-content").focus();
}

function insertChatEmoji(event) {
  const button = event.target.closest("[data-chat-emoji]");
  if (!button) return;
  const input = document.querySelector("#chat-content");
  const emoji = button.dataset.chatEmoji;
  input.setRangeText(emoji, input.selectionStart, input.selectionEnd, "end");
  input.focus();
}

async function submitChatMessage(event) {
  event.preventDefault();
  if (!chatState.activeUserId) return;
  const input = document.querySelector("#chat-content");
  const fileInput = document.querySelector("#chat-file");
  const file = fileInput.files?.[0];
  const content = input.value.trim();
  if (!content && !file) return;

  const form = event.currentTarget;
  const button = form.querySelector("[type=submit]");
  const formData = new FormData();
  formData.set("content", content);
  if (file) formData.set("media", file);
  button.disabled = true;
  document.querySelector("#chat-status").hidden = true;
  try {
    await request(`/chat/${chatState.activeUserId}/messages`, { method: "POST", body: formData });
    input.value = "";
    fileInput.value = "";
    updateChatFileName();
    document.querySelector("#chat-emoji-picker").hidden = true;
    document.querySelector("#chat-emoji-toggle").setAttribute("aria-expanded", "false");
    await loadChatMessages(true);
  } catch (error) {
    notify(document.querySelector("#chat-status"), error.message);
  } finally {
    button.disabled = false;
    input.focus();
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
  await loadStories();
  try {
    const posts = await request("/journey?limit=20");
    if (!posts.length) {
      feed.innerHTML = '<p class="empty-message">Chưa có bài viết nào trong hành trình. Hãy chia sẻ điều đầu tiên của bạn.</p>';
      return;
    }

    feed.innerHTML = posts.map((post) => {
      const authorName = cleanProfileName(post.authorName);
      const authorImage = photoUrl(post.authorPhotoUrl);
      const media = post.mediaUrl
        ? post.mediaType?.startsWith("video/")
          ? `<video class="journey-media" src="${escapeHtml(post.mediaUrl)}" controls playsinline></video>`
          : `<img class="journey-media" src="${escapeHtml(post.mediaUrl)}" alt="Ảnh hành trình của ${escapeHtml(authorName)}">`
        : "";
      const deleteButton = post.isMine
        ? `<button class="journey-delete" data-journey-delete="${post.id}" type="button">Xóa</button>`
        : `<div class="journey-moderation-actions"><button type="button" data-report-user="${post.userId}" data-user-name="${escapeHtml(authorName)}">Báo cáo người dùng</button><button type="button" data-block-user="${post.userId}" data-user-name="${escapeHtml(authorName)}">Chặn</button></div>`;
      const likeLabel = post.isLikedByMe ? "Đã thích" : "Thích";
      const initials = (authorName || "K").trim().charAt(0).toUpperCase();
      return `
        <article class="journey-card" data-journey-post-id="${post.id}">
          <header class="journey-header">
            <div class="journey-author-avatar">${authorImage ? `<img src="${escapeHtml(authorImage)}" alt="">` : escapeHtml(initials)}</div>
            <div class="journey-author-meta">
              <strong>${escapeHtml(authorName)}</strong>
              <time>${new Date(post.createdAt).toLocaleString("vi-VN", { dateStyle: "medium", timeStyle: "short" })}</time>
            </div>
            ${deleteButton}
          </header>
          ${post.content ? `<p class="journey-content">${escapeHtml(post.content)}</p>` : ""}
          ${media}
          <footer class="journey-actions">
            <button class="journey-action-button ${post.isLikedByMe ? "is-liked" : ""}" data-journey-like="${post.id}" type="button">${likeLabel} (${post.likesCount ?? 0})</button>
            <button class="journey-action-button" data-journey-comment-toggle="${post.id}" type="button">Bình luận (${post.commentsCount ?? 0})</button>
            <button class="journey-action-button" data-share-post="${post.id}" type="button">Chia sẻ</button>
            ${post.isMine ? "" : `<button class="journey-action-button" data-report-post="${post.id}" type="button">Báo cáo bài viết</button>`}
          </footer>
          <div class="journey-comment-box" data-journey-comments="${post.id}" hidden>
            <div class="journey-comments" data-journey-comment-list="${post.id}"></div>
            <form class="journey-comment-form" data-journey-comment-form="${post.id}">
              <p class="journey-replying-to" data-replying-to="${post.id}" hidden></p>
              <input type="hidden" name="parentCommentId">
              <input name="content" maxlength="500" placeholder="Viết bình luận..." required>
              <button type="submit">Gửi</button>
              <button class="journey-cancel-reply" data-cancel-reply="${post.id}" type="button" hidden>Hủy trả lời</button>
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
          <div class="journey-comment-item ${comment.parentCommentId ? "is-reply" : ""}" data-comment-id="${comment.id}">
            <strong>${escapeHtml(cleanProfileName(comment.authorName))}</strong>
            <span>${escapeHtml(comment.content)}</span>${comment.userId !== appState.profile?.id ? `<button type="button" data-reply-comment="${comment.id}" data-reply-author="${escapeHtml(comment.authorName)}" data-reply-post="${postId}">Trả lời</button><button type="button" data-report-comment="${comment.id}">Báo cáo</button>` : ""}
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
  const parentCommentId = form.elements.parentCommentId.value || null;
  if (!content || !String(content).trim()) return;
  try {
    await request(`/journey/${postId}/comments`, {
      method: "POST",
      body: JSON.stringify({ content: String(content).trim(), parentCommentId })
    });
    form.reset();
    resetJourneyReply(postId);
    await loadJourney();
    const box = document.querySelector(`[data-journey-comments="${postId}"]`);
    if (box) box.hidden = false;
    notify(document.querySelector("#journey-message"), "Bình luận đã được gửi.", true);
  } catch (error) {
    notify(document.querySelector("#journey-message"), error.message);
  }
}

function prepareJourneyReply(button) {
  const postId = button.dataset.replyPost;
  const form = document.querySelector(`[data-journey-comment-form="${postId}"]`);
  if (!form) return;
  form.elements.parentCommentId.value = button.dataset.replyComment;
  form.elements.content.placeholder = `Trả lời ${button.dataset.replyAuthor}...`;
  const label = form.querySelector(`[data-replying-to="${postId}"]`);
  label.textContent = `Đang trả lời ${button.dataset.replyAuthor}`;
  label.hidden = false;
  form.querySelector(`[data-cancel-reply="${postId}"]`).hidden = false;
  form.elements.content.focus();
}

function resetJourneyReply(postId) {
  const form = document.querySelector(`[data-journey-comment-form="${postId}"]`);
  if (!form) return;
  form.elements.parentCommentId.value = "";
  form.elements.content.placeholder = "Viết bình luận...";
  form.querySelector(`[data-replying-to="${postId}"]`).hidden = true;
  form.querySelector(`[data-cancel-reply="${postId}"]`).hidden = true;
}

async function shareJourneyPost(postId) {
  const card = document.querySelector(`[data-journey-post-id="${CSS.escape(postId)}"]`);
  const url = new URL(window.location.href);
  url.search = "";
  url.searchParams.set("post", postId);
  const description = card?.querySelector(".journey-content")?.textContent || "Xem bài viết trên Kiss";
  try {
    if (navigator.share) await navigator.share({ title: "Bài viết trên Kiss", text: description, url: url.toString() });
    else {
      await navigator.clipboard.writeText(url.toString());
      notify(document.querySelector("#journey-message"), "Đã sao chép liên kết bài viết.", true);
    }
  } catch (error) {
    if (error.name !== "AbortError") notify(document.querySelector("#journey-message"), "Không thể chia sẻ liên kết trên thiết bị này.");
  }
}

async function loadStories() {
  const container = document.querySelector("#story-list");
  try {
    const stories = await request("/stories");
    container.innerHTML = stories.length ? stories.map((story) => `
      <article class="story-card">
        <header><strong>${escapeHtml(cleanProfileName(story.authorName))}</strong><time>Hết hạn ${new Date(story.expiresAt).toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}</time></header>
        <p>${escapeHtml(story.content)}</p>
        ${story.isMine ? `<button type="button" data-delete-story="${story.id}">Xóa story</button>` : ""}
      </article>`).join("") : '<p class="session-empty">Story của bạn và người đã kết đôi sẽ xuất hiện ở đây.</p>';
  } catch (error) {
    container.innerHTML = `<p class="session-empty">${escapeHtml(error.message)}</p>`;
  }
}

async function submitStory(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const content = form.elements.content.value.trim();
  if (!content) return;
  try {
    await request("/stories", { method: "POST", body: JSON.stringify({ content }) });
    form.reset();
    await loadStories();
  } catch (error) {
    notify(document.querySelector("#journey-message"), error.message);
  }
}

async function manageStory(event) {
  const button = event.target.closest("[data-delete-story]");
  if (!button) return;
  try {
    await request(`/stories/${button.dataset.deleteStory}`, { method: "DELETE" });
    await loadStories();
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
  stopPresenceHeartbeat();
  stopChatPolling();
  stopSocialPolling();
  clearChatMediaUrls();
  chatState.contacts = [];
  chatState.activeUserId = null;
  chatState.pendingUserId = null;
  localStorage.removeItem("kiss.accessToken");
  localStorage.removeItem("kiss.profile");
  appState.token = null;
  appState.profile = null;
  appState.isOwner = false;
  document.querySelector("#events-nav").hidden = true;
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
  document.querySelector("#discover-location-button").addEventListener("click", updateDiscoveryLocation);
  document.querySelector("#profile-form").addEventListener("submit", saveProfile);
  document.querySelector("#profile-tips-button").addEventListener("click", suggestProfileImprovements);
  document.querySelector("#privacy-form").addEventListener("submit", savePrivacySettings);
  document.querySelector("#notification-preferences-form").addEventListener("submit", saveNotificationPreferences);
  document.querySelector("#notification-list").addEventListener("click", manageNotifications);
  document.querySelector("#refresh-notifications").addEventListener("click", loadNotifications);
  document.querySelector("#mark-all-notifications-read").addEventListener("click", markAllNotificationsRead);
  document.querySelector("#enable-browser-notifications").addEventListener("click", enableBrowserNotifications);
  document.querySelector("#date-invitation-form").addEventListener("submit", submitDateInvitation);
  document.querySelector("#date-list").addEventListener("click", manageDateInvitation);
  document.querySelector("#date-list").addEventListener("submit", submitDateReview);
  document.querySelector("#dates-refresh").addEventListener("click", loadDates);
  document.querySelector("#community-event-form").addEventListener("submit", submitCommunityEvent);
  document.querySelector("#community-event-list").addEventListener("click", manageCommunityEvent);
  document.querySelector("#community-events-refresh").addEventListener("click", loadCommunityEvents);
  document.querySelector("#change-password-form").addEventListener("submit", submitPasswordChange);
  document.querySelector("#request-email-code").addEventListener("click", requestEmailVerificationCode);
  document.querySelector("#email-verification-form").addEventListener("submit", submitEmailVerification);
  document.querySelector("#session-list").addEventListener("click", manageSessions);
  document.querySelector("#revoke-other-sessions").addEventListener("click", revokeOtherSessions);
  document.querySelector("#photo-form").addEventListener("submit", uploadPhoto);
  document.querySelector("#photo-list").addEventListener("click", managePhoto);
  document.querySelector("#discover-results").addEventListener("click", swipe);
  document.querySelector("#chat-messages").addEventListener("click", manageChatMessage);
  document.querySelector("#discover-pagination").addEventListener("click", (event) => {
    const button = event.target.closest("[data-discover-step]");
    if (!button || button.disabled) return;
    appState.discoverPage += Number(button.dataset.discoverStep);
    renderDiscoverPage();
    document.querySelector("#discover-filters").scrollIntoView({ behavior: "smooth", block: "start" });
  });
  document.querySelector("#discover-pagination").addEventListener("change", (event) => {
    const select = event.target.closest("[data-discover-page-size]");
    if (!select) return;
    appState.discoverPageSize = Number(select.value);
    appState.discoverPage = 1;
    renderDiscoverPage();
  });
  document.querySelector("#matches-results").addEventListener("click", openChatFromMatch);
  document.querySelector("#passed-results").addEventListener("click", undoPass);
  document.querySelector("#chat-contact-list").addEventListener("click", (event) => {
    const contact = event.target.closest("[data-chat-user]");
    if (contact) selectChatContact(contact.dataset.chatUser);
  });
  document.querySelector("#chat-form").addEventListener("submit", submitChatMessage);
  document.querySelector("#chat-file").addEventListener("change", updateChatFileName);
  document.querySelector("#chat-file-clear").addEventListener("click", () => {
    document.querySelector("#chat-file").value = "";
    updateChatFileName();
  });
  document.querySelector("#chat-emoji-toggle").addEventListener("click", (event) => {
    const picker = document.querySelector("#chat-emoji-picker");
    picker.hidden = !picker.hidden;
    event.currentTarget.setAttribute("aria-expanded", String(!picker.hidden));
  });
  document.querySelector("#chat-emoji-picker").addEventListener("click", insertChatEmoji);
  document.querySelector("#chat-gift-toggle").addEventListener("click", toggleChatGiftPicker);
  document.querySelector("#chat-gift-picker").addEventListener("click", sendChatGift);
  document.querySelector("#chat-suggestion-toggle").addEventListener("click", showChatSuggestions);
  document.querySelector("#chat-suggestion-picker").addEventListener("click", selectChatSuggestion);
  document.querySelector("#chat-content").addEventListener("keydown", (event) => {
    if (event.key === "Enter" && !event.shiftKey && !event.isComposing) {
      event.preventDefault();
      event.currentTarget.form.requestSubmit();
    }
  });
  document.querySelector("#journey-form").addEventListener("submit", submitJourneyPost);
  document.querySelector("#story-form").addEventListener("submit", submitStory);
  document.querySelector("#story-list").addEventListener("click", manageStory);
  document.querySelector("#journey-media").addEventListener("change", updateJourneyPreview);
  document.querySelector("#journey-refresh").addEventListener("click", loadJourney);
  document.querySelector("#journey-feed").addEventListener("click", deleteJourneyPost);
  document.querySelector("#journey-feed").addEventListener("click", (event) => {
    const reportUser = event.target.closest("[data-report-user]");
    const block = event.target.closest("[data-block-user]");
    const reportPost = event.target.closest("[data-report-post]");
    const reportComment = event.target.closest("[data-report-comment]");
    const replyComment = event.target.closest("[data-reply-comment]");
    const cancelReply = event.target.closest("[data-cancel-reply]");
    const sharePost = event.target.closest("[data-share-post]");
    if (reportUser) reportContent("User", reportUser.dataset.reportUser);
    else if (block) blockUser(block.dataset.blockUser, block.dataset.userName);
    else if (reportPost) reportContent("Post", reportPost.dataset.reportPost);
    else if (reportComment) reportContent("Comment", reportComment.dataset.reportComment);
    else if (replyComment) prepareJourneyReply(replyComment);
    else if (cancelReply) resetJourneyReply(cancelReply.dataset.cancelReply);
    else if (sharePost) shareJourneyPost(sharePost.dataset.sharePost);
  });
  document.querySelector("#chat-messages").addEventListener("click", (event) => {
    const button = event.target.closest("[data-report-message]");
    if (button) reportContent("Message", button.dataset.reportMessage);
  });
  document.querySelector("#chat-peer-block").addEventListener("click", (event) => blockUser(event.currentTarget.dataset.blockUser, document.querySelector("#chat-peer-name").textContent));
  document.querySelector("#dating-app").addEventListener("click", (event) => {
    const card = event.target.closest(".person-card");
    if (!card) return;
    const report = event.target.closest("[data-report-user]");
    const block = event.target.closest("[data-block-user]");
    if (report) reportContent("User", report.dataset.reportUser);
    if (block) blockUser(block.dataset.blockUser, block.dataset.userName);
  });
  document.querySelector("#blocked-users").addEventListener("click", (event) => {
    const button = event.target.closest("[data-unblock-user]");
    if (button) unblockUser(button.dataset.unblockUser);
  });
  document.querySelector("#people-search-form").addEventListener("submit", (event) => { event.preventDefault(); loadConnections(event.currentTarget.elements.q.value.trim()); });
  document.querySelector("#connections-refresh").addEventListener("click", () => loadConnections());
  document.querySelector("#connections-page").addEventListener("click", manageConnection);
  document.querySelector("#conversation-list").addEventListener("click", (event) => {
    const button = event.target.closest("[data-open-conversation]");
    if (!button) return;
    appState.selectedConversationId = button.dataset.openConversation;
    renderConversations();
    loadSocialMessages();
  });
  document.querySelector("#social-chat-form").addEventListener("submit", submitSocialMessage);
  document.querySelector("#social-chat-form textarea").addEventListener("keydown", (event) => {
    if (event.key === "Enter" && !event.shiftKey && !event.isComposing) {
      event.preventDefault();
      event.currentTarget.form.requestSubmit();
    }
  });
  document.querySelectorAll("[data-page]").forEach((button) => button.addEventListener("click", () => showPage(button.dataset.page)));
  document.querySelector("#logout-button").addEventListener("click", async () => {
    try { await request("/auth/logout", { method: "POST" }); } catch { /* Clear local credentials even if the network is unavailable. */ }
    logout();
  });
  document.querySelector("#forgot-password-link").addEventListener("click", showForgotPassword);
  document.querySelector("#forgot-password-back").addEventListener("click", () => setAuthMode("login"));
  document.querySelector("#reset-password-back").addEventListener("click", () => setAuthMode("login"));
  forgotPasswordForm.addEventListener("submit", submitForgotPassword);
  resetPasswordForm.addEventListener("submit", submitResetPassword);
  document.querySelector("#admin-refresh").addEventListener("click", loadAdminOverview);
  document.querySelector("#admin-reports-refresh").addEventListener("click", loadAdminOverview);
  document.querySelector("#admin-export").addEventListener("click", exportAdminData);
  document.querySelector("#admin-announcement-form").addEventListener("submit", sendSystemAnnouncement);
  document.querySelector("#admin-user-form").addEventListener("submit", submitAdminUser);
  document.querySelector("#admin-user-cancel").addEventListener("click", resetAdminUserForm);
  document.querySelector("#admin-users").addEventListener("click", (event) => {
    const editButton = event.target.closest("[data-admin-edit]");
    if (editButton) {
      editAdminUser(editButton.dataset.adminEdit);
      return;
    }
    const deleteButton = event.target.closest("[data-admin-delete]");
    if (deleteButton && !deleteButton.disabled) deleteAdminUser(deleteButton.dataset.adminDelete);
  });
  document.querySelector("#admin-reports").addEventListener("click", (event) => {
    const resolve = event.target.closest("[data-report-resolve]");
    const dismiss = event.target.closest("[data-report-dismiss]");
    if (resolve) updateAdminReport(resolve.dataset.reportResolve, true);
    if (dismiss) updateAdminReport(dismiss.dataset.reportDismiss, false);
  });

  const resetUrl = new URL(window.location.href);
  const resetToken = resetUrl.searchParams.get("resetToken");
  if (resetToken) {
    resetUrl.searchParams.delete("resetToken");
    window.history.replaceState({}, "", `${resetUrl.pathname}${resetUrl.search}${resetUrl.hash}`);
    showResetPassword(resetToken);
    return;
  }

  if (!appState.token) return;
  try {
    appState.profile = await request("/profiles/me");
    await enterWorkspace();
  } catch {
    logout();
  }
}

startApp();
