-- test 1: no results
select *
from videos.messages m
where 
	(m.photo is not null)
	and (m.mimetype like 'video/%' and isnull(mediatype, 'null') in ('video_file', 'null'))

-- test 2: no results
select *
from (
	select chatid, id messageid,date
	from videos.messages m
	where 
		(m.photo is not null)
		or (m.mimetype like 'video/%' and isnull(mediatype, 'null') in ('video_file', 'null'))
) m
left join videos.uploadeditems u on u.chatid = m.chatid and u.messageid = m.messageid
where u.id is null

-- test 3: no results
select m.*, u.telegrampublishedon, u.status, u.statuscode, u.statusmessage, u.mediaitemid, u.mediaitemmetadatacreationtime, u.filenametimestamp
from (
	select chatid, id messageid,date, case when m.photo is not null then 'photo' else 'video' end type
	from videos.messages m
	where 
		(m.photo is not null)
		or (m.mimetype like 'video/%' and isnull(mediatype, 'null') in ('video_file', 'null'))
) m
left join videos.uploadeditems u on u.chatid = m.chatid and u.messageid = m.messageid
where m.date <> u.telegrampublishedon

-- test 4: no results
select m.*, u.telegrampublishedon, u.status, u.statuscode, u.statusmessage, u.mediaitemid, u.mediaitemmetadatacreationtime, u.filenametimestamp
from (
	select chatid, id messageid,date, case when m.photo is not null then 'photo' else 'video' end type
	from videos.messages m
	where 
		(m.photo is not null)
		or (m.mimetype like 'video/%' and isnull(mediatype, 'null') in ('video_file', 'null'))
) m
left join videos.uploadeditems u on u.chatid = m.chatid and u.messageid = m.messageid
where filenametimestamp <> mediaitemmetadatacreationtime

-- test 5: no results
select m.*, u.status, u.statuscode, u.statusmessage, u.mediaitemid, u.mediaitemmetadatacreationtime, 
	datediff(minute, mediaitemmetadatacreationtime, date) minutes, datediff(day, mediaitemmetadatacreationtime, date) days
	, i.filename, i.isphoto, i.isvideo, i.mediametadatacreationtime, i.mediametadatavideostatus, i.producturl
from (
	select chatid, id messageid,date, case when m.photo is not null then 'photo' else 'video' end type
	from videos.messages m
	where 
		(m.photo is not null)
		or (m.mimetype like 'video/%' and isnull(mediatype, 'null') in ('video_file', 'null'))
) m
left join videos.uploadeditems u on u.chatid = m.chatid and u.messageid = m.messageid
left join videos.inventoryitems i on i.id = u.mediaitemid
where i.mediametadatacreationtime <> mediaitemmetadatacreationtime

-- test 6: no results
select m.*, u.status, u.statuscode, u.statusmessage, u.mediaitemid, u.mediaitemmetadatacreationtime, 
	datediff(minute, mediaitemmetadatacreationtime, date) minutes, datediff(day, mediaitemmetadatacreationtime, date) days
	, i.filename, i.isphoto, i.isvideo, i.mediametadatacreationtime, i.mediametadatavideostatus, i.producturl
from (
	select chatid, id messageid,date, case when m.photo is not null then 'photo' else 'video' end type
	from videos.messages m
	where 
		(m.photo is not null)
		or (m.mimetype like 'video/%' and isnull(mediatype, 'null') in ('video_file', 'null'))
) m
left join videos.uploadeditems u on u.chatid = m.chatid and u.messageid = m.messageid
left join videos.inventoryitems i on i.id = u.mediaitemid
where date < mediaitemmetadatacreationtime

-- test 7: no results
select m.*, u.status, u.statuscode, u.statusmessage, u.mediaitemid, u.mediaitemmetadatacreationtime, 
	datediff(minute, mediaitemmetadatacreationtime, date) minutes, datediff(day, mediaitemmetadatacreationtime, date) days
	, i.filename, i.isphoto, i.isvideo, i.mediametadatacreationtime, i.mediametadatavideostatus, i.producturl
from (
	select chatid, id messageid,date, case when m.photo is not null then 'photo' else 'video' end type
	from videos.messages m
	where 
		(m.mimetype like 'video/%' and isnull(mediatype, 'null') in ('video_file', 'null'))
) m
left join videos.uploadeditems u on u.chatid = m.chatid and u.messageid = m.messageid
left join videos.inventoryitems i on i.id = u.mediaitemid
where isnull(mediametadatavideostatus, 'null') <> 'READY'

-- test 8: no results
-- just a logic test that the recursive cte is correct
;with m as (
	select chatid, id, replytomessageid, 1 as level
	from videos.messages m
	where 
		(m.photo is not null)
		or (m.mimetype like 'video/%' and isnull(mediatype, 'null') in ('video_file', 'null'))
), r as (
	select chatid, id childid, replytomessageid parentid, 1 as level, 0 as isfinal
	from m
	where replytomessageid is not null
	union all
	select p.chatid, r.childid, isnull(p.replytomessageid, p.id) parentid, level + 1 as level, case when p.replytomessageid is null then 1 else 0 end isfinal
	from videos.messages p
	inner join r on r.parentid = p.id and r.chatid = p.chatid
	where r.isfinal = 0
), roots as (
	select *
	from r
	where isfinal = 1
)

select 1
from r
where isfinal = 1
group by chatid, childid
having count(1) > 1
union all
select 1
from m
left join roots r on r.childid = m.id and r.chatid = m.chatid
left join videos.messages rm on rm.chatid = m.chatid and rm.id = r.parentid
where m.replytomessageid is not null and r.chatid is null
	or rm.replytomessageid is not null
	or rm.action <> 'topic_created'

-- test 9: no results
select *
from videos.UploadedItems u
full join (

select * from videos.Messages where topicid is not null

) m on m.chatid = u.chatid and m.id = u.messageid
where u.id is null or m.id is null
